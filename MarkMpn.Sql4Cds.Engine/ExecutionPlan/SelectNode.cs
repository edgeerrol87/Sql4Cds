﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarkMpn.Sql4Cds.Engine.FetchXml;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Newtonsoft.Json;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    /// <summary>
    /// Converts a data stream to a full data table
    /// </summary>
    class SelectNode : BaseNode, ISingleSourceExecutionPlanNode, IDataReaderExecutionPlanNode
    {
        private int _executionCount;
        private readonly Timer _timer = new Timer();

        /// <summary>
        /// The columns that should be included in the query results
        /// </summary>
        [Category("Select")]
        [Description("The columns that should be included in the query results")]
        [DisplayName("Column Set")]
        public List<SelectColumn> ColumnSet { get; } = new List<SelectColumn>();

        /// <summary>
        /// The data source to select from
        /// </summary>
        [Browsable(false)]
        public IDataExecutionPlanNodeInternal Source { get; set; }

        /// <summary>
        /// The schema that should be used for expanding "*" columns
        /// </summary>
        [Browsable(false)]
        [JsonIgnore]
        public INodeSchema LogicalSourceSchema { get; set; }

        [Browsable(false)]
        public string Sql { get; set; }

        [Browsable(false)]
        public int Index { get; set; }

        [Browsable(false)]
        public int Length { get; set; }

        [Browsable(false)]
        public int LineNumber { get; set; }

        public override TimeSpan Duration => _timer.Duration;

        public override int ExecutionCount => _executionCount;

        public DbDataReader Execute(NodeExecutionContext context, CommandBehavior behavior)
        {
            _executionCount++;

            var timer = _timer.Run();
            var schema = Source.GetSchema(context);
            var source = behavior.HasFlag(CommandBehavior.SchemaOnly) ? Array.Empty<Entity>() : Source.Execute(context);

            if (behavior.HasFlag(CommandBehavior.SingleRow))
                source = source.Take(1);

            return new SelectDataReader(ColumnSet, timer, schema, source, context.ParameterValues);
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            yield return Source;
        }

        public IRootExecutionPlanNodeInternal[] FoldQuery(NodeCompilationContext context, IList<OptimizerHint> hints)
        {
            Source = Source.FoldQuery(context, hints);
            Source.Parent = this;

            FoldFetchXmlColumns(Source, ColumnSet, context);
            FoldMetadataColumns(Source, ColumnSet);

            ExpandWildcardColumns(context);

            if (Source is AliasNode alias)
            {
                var aliasColumns = alias.ColumnSet.ToDictionary(col => alias.Alias.EscapeIdentifier() + "." + col.OutputColumn, col => col.SourceColumn);

                foreach (var col in ColumnSet)
                    col.SourceColumn = aliasColumns[col.SourceColumn];

                Source = alias.Source;
                Source.Parent = this;
            }

            return new[] { this };
        }

        internal static void FoldFetchXmlColumns(IDataExecutionPlanNode source, List<SelectColumn> columnSet, NodeCompilationContext context)
        {
            if (source is FetchXmlScan fetchXml)
            {
                if (!context.DataSources.TryGetValue(fetchXml.DataSource, out var dataSource))
                    throw new NotSupportedQueryFragmentException("Missing datasource " + fetchXml.DataSource);

                // Check if there are any aliases we can apply to the source FetchXml
                var schema = fetchXml.GetSchema(context);
                var hasStar = columnSet.Any(col => col.AllColumns && col.SourceColumn == null) && fetchXml.HiddenAliases.Count == 0;
                var aliasStars = new HashSet<string>(columnSet.Where(col => col.AllColumns && col.SourceColumn != null).Select(col => col.SourceColumn.Replace(".*", "")).Distinct(StringComparer.OrdinalIgnoreCase).Except(fetchXml.HiddenAliases), StringComparer.OrdinalIgnoreCase);

                foreach (var col in columnSet)
                {
                    if (col.AllColumns)
                    {
                        if (col.SourceColumn == null)
                        {
                            // Add an all-attributes to the main entity and all link-entities
                            if (!fetchXml.HiddenAliases.Contains(fetchXml.Alias))
                                fetchXml.Entity.AddItem(new allattributes());

                            foreach (var link in fetchXml.Entity.GetLinkEntities())
                            {
                                if (link.SemiJoin || fetchXml.HiddenAliases.Contains(link.alias))
                                    continue;

                                if (fetchXml.HiddenAliases.Contains(link.alias))
                                    continue;

                                link.AddItem(new allattributes());
                            }
                        }
                        else if (!hasStar && !fetchXml.HiddenAliases.Contains(col.SourceColumn.Replace(".*", "")))
                        {
                            // Only add an all-attributes to the appropriate entity/link-entity
                            if (col.SourceColumn.Replace(".*", "").Equals(fetchXml.Alias, StringComparison.OrdinalIgnoreCase))
                            {
                                fetchXml.Entity.AddItem(new allattributes());
                            }
                            else
                            {
                                var link = fetchXml.Entity.FindLinkEntity(col.SourceColumn.Replace(".*", ""));
                                link.AddItem(new allattributes());
                            }
                        }
                    }
                    else if (!hasStar)
                    {
                        // Only fold individual columns down to the FetchXML if there is no corresponding all-attributes
                        var parts = col.SourceColumn.SplitMultiPartIdentifier();

                        if (parts.Length == 1 || !aliasStars.Contains(parts[0]))
                        {
                            var sourceCol = col.SourceColumn;
                            schema.ContainsColumn(sourceCol, out sourceCol);
                            fetchXml.AddAttribute(sourceCol, null, dataSource.Metadata, out _, out _);
                        }
                    }
                }

                // Finally, check what aliases we can fold down to the FetchXML.
                // Ignore:
                // 1. columns that have more than 1 alias
                // 2. aliases that are invalid for FetchXML
                // 3. attributes that are included via an <all-attributes/>
                // 4. virtual ___name or ___type attributes
                if (!hasStar)
                {
                    var aliasedColumns = columnSet
                        .Where(c => !c.AllColumns)
                        .Select(c =>
                        {
                            var sourceCol = c.SourceColumn;
                            schema.ContainsColumn(sourceCol, out sourceCol);

                            return new { Mapping = c, SourceColumn = sourceCol, Alias = c.OutputColumn };
                        })
                        .Select(c =>
                        {
                            // Check which underlying attribute the data is coming from, handling virtual attributes
                            var parts = c.SourceColumn.SplitMultiPartIdentifier();
                            var entityName = fetchXml.Entity.name;
                            var attrName = parts.Last();

                            if (parts.Length > 1 && !parts[0].Equals(fetchXml.Alias))
                                entityName = fetchXml.Entity.FindLinkEntity(parts[0])?.name;

                            if (entityName == null)
                                return null;

                            var metadata = dataSource.Metadata;
                            var meta = metadata[entityName].Attributes.SingleOrDefault(a => a.LogicalName.Equals(attrName, StringComparison.OrdinalIgnoreCase) && a.AttributeOf == null);
                            var isVirtual = false;
                            if (meta == null)
                            {
                                meta = metadata[entityName].FindBaseAttributeFromVirtualAttribute(attrName, out _);
                                if (meta != null)
                                    isVirtual = true;
                            }

                            return new { c.Mapping, c.SourceColumn, c.Alias, meta?.LogicalName, IsVirtual = isVirtual };
                        })
                        .Where(c => c?.LogicalName != null) // Ignore attributes we can't find in the metadata
                        .GroupBy(c => c.LogicalName, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() == 1) // Ignore attributes that appear multiple times, either as physical or virtual attributes
                        .Select(g => g.Single())
                        .Where(c => c.IsVirtual == false) // Ignore virtual attributes
                        .GroupBy(c => c.Alias, StringComparer.OrdinalIgnoreCase)
                        .Where(g => g.Count() == 1) // Don't fold aliases if there are multiple columns using the same alias
                        .Select(g => g.Single())
                        .Where(c =>
                        {
                            var parts = c.SourceColumn.SplitMultiPartIdentifier();

                            if (parts.Length > 1 && aliasStars.Contains(parts[0]))
                                return false; // Don't fold aliases if we're using an <all-attributes/>

                            if (c.Alias.Equals(parts.Last(), StringComparison.OrdinalIgnoreCase))
                                return false; // Don't fold aliases if we're using the original source name

                            if (!FetchXmlScan.IsValidAlias(c.Alias))
                                return false; // Don't fold aliases if they contain invalid characters

                            if (fetchXml.ColumnMappings.Any(m => m.OutputColumn == c.SourceColumn))
                                return false; // Don't fold aliases if they're already aliased in the FetchXmlScan node

                            return true;
                        })
                        .Select(c =>
                        {
                            var attr = fetchXml.AddAttribute(c.SourceColumn, null, dataSource.Metadata, out _, out var linkEntity);
                            return new { c.Mapping, c.SourceColumn, c.Alias, Attr = attr, LinkEntity = linkEntity };
                        })
                        .Where(c =>
                        {
                            var items = c.LinkEntity?.Items ?? fetchXml.Entity.Items;

                            // Don't fold the alias if there's also a sort on the same attribute, as it breaks paging
                            // https://markcarrington.dev/2019/12/10/inside-fetchxml-pt-4-order/#sorting_&_aliases
                            if (items != null && items.OfType<FetchOrderType>().Any(order => order.attribute == c.Attr.name) && fetchXml.AllPages)
                                return false;

                            // Don't fold the alias if it's on the audit table, it seems to break the provider
                            if (c.LinkEntity != null && c.LinkEntity.name == "audit" ||
                                c.LinkEntity == null && fetchXml.Entity.name == "audit")
                                return false;

                            return true;
                        })
                        .ToList();

                    foreach (var aliasedColumn in aliasedColumns)
                    {
                        aliasedColumn.Attr.alias = aliasedColumn.Alias;
                        aliasedColumn.Mapping.SourceColumn = aliasedColumn.SourceColumn.SplitMultiPartIdentifier()[0] + "." + aliasedColumn.Alias;
                    }
                }
            }
        }

        private void FoldMetadataColumns(IDataExecutionPlanNode source, List<SelectColumn> columnSet)
        {
            if (source is MetadataQueryNode metadata)
            {
                // Check if there are any wildcard columns we can apply to the source metadata query
                var hasStar = columnSet.Any(col => col.AllColumns && col.SourceColumn == null);
                var aliasStars = new HashSet<string>(columnSet.Where(col => col.AllColumns && col.SourceColumn != null).Select(col => col.SourceColumn.Replace(".*", "")).Distinct(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

                if (metadata.MetadataSource.HasFlag(MetadataSource.Entity) && (hasStar || aliasStars.Contains(metadata.EntityAlias)))
                {
                    if (metadata.Query.Properties == null)
                        metadata.Query.Properties = new MetadataPropertiesExpression();

                    metadata.Query.Properties.AllProperties = true;
                }

                if (metadata.MetadataSource.HasFlag(MetadataSource.Attribute) && (hasStar || aliasStars.Contains(metadata.AttributeAlias)))
                {
                    if (metadata.Query.AttributeQuery == null)
                        metadata.Query.AttributeQuery = new AttributeQueryExpression();

                    if (metadata.Query.AttributeQuery.Properties == null)
                        metadata.Query.AttributeQuery.Properties = new MetadataPropertiesExpression();

                    metadata.Query.AttributeQuery.Properties.AllProperties = true;
                }

                if ((metadata.MetadataSource.HasFlag(MetadataSource.OneToManyRelationship) && (hasStar || aliasStars.Contains(metadata.OneToManyRelationshipAlias))) ||
                    (metadata.MetadataSource.HasFlag(MetadataSource.ManyToOneRelationship) && (hasStar || aliasStars.Contains(metadata.ManyToOneRelationshipAlias))) ||
                    (metadata.MetadataSource.HasFlag(MetadataSource.ManyToManyRelationship) && (hasStar || aliasStars.Contains(metadata.ManyToManyRelationshipAlias))))
                {
                    if (metadata.Query.RelationshipQuery == null)
                        metadata.Query.RelationshipQuery = new RelationshipQueryExpression();

                    if (metadata.Query.RelationshipQuery.Properties == null)
                        metadata.Query.RelationshipQuery.Properties = new MetadataPropertiesExpression();

                    metadata.Query.RelationshipQuery.Properties.AllProperties = true;
                }

                if (metadata.MetadataSource.HasFlag(MetadataSource.Key) && (hasStar || aliasStars.Contains(metadata.KeyAlias)))
                {
                    if (metadata.Query.KeyQuery == null)
                        metadata.Query.KeyQuery = new EntityKeyQueryExpression();

                    if (metadata.Query.KeyQuery.Properties == null)
                        metadata.Query.KeyQuery.Properties = new MetadataPropertiesExpression();

                    metadata.Query.KeyQuery.Properties.AllProperties = true;
                }
            }
        }

        public void ExpandWildcardColumns(NodeCompilationContext context)
        {
            ExpandWildcardColumns(Source, LogicalSourceSchema, ColumnSet, context);
        }

        internal static void ExpandWildcardColumns(IDataExecutionPlanNodeInternal source, INodeSchema sourceSchema, List<SelectColumn> columnSet, NodeCompilationContext context)
        {
            // Expand any AllColumns
            if (columnSet.Any(col => col.AllColumns))
            {
                var schema = source.GetSchema(context);
                var expanded = new List<SelectColumn>();

                foreach (var col in columnSet)
                {
                    if (!col.AllColumns)
                    {
                        expanded.Add(col);
                        continue;
                    }

                    foreach (var src in sourceSchema.Schema.Where(k => k.Value.IsVisible && (col.SourceColumn == null || k.Key.StartsWith(col.SourceColumn + ".", StringComparison.OrdinalIgnoreCase))).Select(k => k.Key))
                    {
                        // Columns might be available in the logical source schema but not in
                        // the real one, e.g. due to aggregation
                        if (!schema.ContainsColumn(src, out _))
                            src.ToColumnReference().GetType(new ExpressionCompilationContext(context, schema, sourceSchema), out _);

                        expanded.Add(new SelectColumn
                        {
                            SourceColumn = src,
                            OutputColumn = src.SplitMultiPartIdentifier().Last()
                        });
                    }
                }

                columnSet.Clear();
                columnSet.AddRange(expanded);
            }
        }

        public override void AddRequiredColumns(NodeCompilationContext context, IList<string> requiredColumns)
        {
            foreach (var col in ColumnSet.Select(c => c.SourceColumn + (c.AllColumns ? ".*" : "")))
            {
                if (!requiredColumns.Contains(col, StringComparer.OrdinalIgnoreCase))
                    requiredColumns.Add(col);
            }

            Source.AddRequiredColumns(context, requiredColumns);
        }

        public override string ToString()
        {
            return "SELECT";
        }

        public object Clone()
        {
            var clone = new SelectNode
            {
                Source = (IDataExecutionPlanNodeInternal)Source.Clone(),
                LogicalSourceSchema = LogicalSourceSchema,
                Sql = Sql,
                Index = Index,
                Length = Length,
                LineNumber = LineNumber,
            };

            clone.ColumnSet.AddRange(ColumnSet);
            clone.Source.Parent = clone;

            return clone;
        }
    }

    /// <summary>
    /// Describes a column generated by a <see cref="SelectNode"/>
    /// </summary>
    class SelectColumn
    {
        /// <summary>
        /// The name of the column in the source data
        /// </summary>
        [Description("The names of the column in the source node that generates the data for this column")]
        public string SourceColumn { get; set; }

        /// <summary>
        /// The expression that provides the value for the column
        /// </summary>
        /// <remarks>
        /// Used for error reporting only
        /// </remarks>
        [Browsable(false)]
        public TSqlFragment SourceExpression { get; set; }

        /// <summary>
        /// The requested name of the column in the output data
        /// </summary>
        [Description("The name of the column that is generated in the output")]
        [DictionaryKey]
        public string OutputColumn { get; set; }

        /// <summary>
        /// Indicates this is a placeholder for all columns from the source data (SELECT *)
        /// </summary>
        [Browsable(false)]
        public bool AllColumns { get; set; }
    }
}
