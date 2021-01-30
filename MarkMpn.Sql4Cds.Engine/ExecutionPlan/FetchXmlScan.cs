﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using MarkMpn.Sql4Cds.Engine.FetchXml;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    public class FetchXmlScan : BaseNode
    {
        public FetchXmlScan()
        {
            AllPages = true;
        }

        /// <summary>
        /// The FetchXML query
        /// </summary>
        public FetchType FetchXml { get; set; }

        /// <summary>
        /// The main &lt;entity&gt; node in the <see cref="FetchXml"/>
        /// </summary>
        public FetchEntityType Entity => FetchXml.Items.OfType<FetchEntityType>().Single();

        /// <summary>
        /// The alias to apply to the primary entity in the query
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// The string representation of the <see cref="FetchXml"/>
        /// </summary>
        public string FetchXmlString => Serialize(FetchXml);

        /// <summary>
        /// Indicates if the query will page across all the available data
        /// </summary>
        public bool AllPages { get; set; }

        /// <summary>
        /// Indicates if this query uses the <see cref="FetchType.distinct"/> option without having a sort order specified
        /// </summary>
        public bool DistinctWithoutSort => FetchXml.distinctSpecified && FetchXml.distinct && !ContainsSort(FetchXml.Items);

        /// <summary>
        /// Indicates if all available attributes should be returned as part of the schema, used while the execution plan is being built
        /// </summary>
        public bool ReturnFullSchema { get; set; }

        public override IEnumerable<Entity> Execute(IOrganizationService org, IAttributeMetadataCache metadata, IQueryExecutionOptions options)
        {
            if (options.Cancelled)
                yield break;

            var mainEntity = FetchXml.Items.OfType<FetchEntityType>().Single();
            var name = mainEntity.name;
            var meta = metadata[name];
            options.Progress(0, $"Retrieving {GetDisplayName(0, meta)}...");

            // Get the first page of results
            var res = org.RetrieveMultiple(new FetchExpression(Serialize(FetchXml)));

            foreach (var entity in res.Entities)
            {
                // Expose any formatted values for OptionSetValue and EntityReference values
                foreach (var formatted in entity.FormattedValues)
                {
                    if (!entity.Contains(formatted.Key + "name"))
                        entity[formatted.Key + "name"] = formatted.Value;
                }

                // Expose the type of lookup values
                foreach (var attribute in entity.Attributes.Where(attr => attr.Value is EntityReference).ToList())
                {
                    if (!entity.Contains(attribute.Key + "type"))
                        entity[attribute.Key + "type"] = ((EntityReference)attribute.Value).LogicalName;
                }

                yield return entity;
            }

            var count = res.Entities.Count;

            // Aggregate queries return up to 5000 records and don't provide a method to move on to the next page
            // Throw an exception to indicate the error to the caller
            if (AllPages && FetchXml.aggregateSpecified && FetchXml.aggregate && count == 5000 && FetchXml.top != "5000" && !res.MoreRecords)
                throw new ApplicationException("AggregateQueryRecordLimit");

            // Distinct queries without a sort order can't be reliably paged. Throw an exception to get the user
            // to apply a useful sort order
            if (AllPages && DistinctWithoutSort)
                throw new ApplicationException("DISTINCT queries must have an ORDER BY applied to retrieve multiple pages\r\nSee https://docs.microsoft.com/powerapps/developer/common-data-service/org-service/paging-behaviors-and-ordering#ordering-with-a-paging-cookie");

            // Move on to subsequent pages
            while (AllPages && res.MoreRecords && !options.Cancelled && options.ContinueRetrieve(count))
            {
                options.Progress(0, $"Retrieved {count:N0} {GetDisplayName(count, meta)}...");

                if (FetchXml.page == null)
                    FetchXml.page = "2";
                else
                    FetchXml.page = (Int32.Parse(FetchXml.page) + 1).ToString();

                FetchXml.pagingcookie = res.PagingCookie;

                var nextPage = org.RetrieveMultiple(new FetchExpression(Serialize(FetchXml)));

                foreach (var entity in nextPage.Entities)
                    yield return entity;

                count += nextPage.Entities.Count;
                res = nextPage;
            }
        }

        private bool ContainsSort(object[] items)
        {
            if (items == null)
                return false;

            if (items.OfType<FetchOrderType>().Any())
                return true;

            if (items.OfType<FetchEntityType>().Any(entity => ContainsSort(entity.Items)))
                return true;

            if (items.OfType<FetchLinkEntityType>().Any(linkEntity => ContainsSort(linkEntity.Items)))
                return true;

            return false;
        }

        /// <summary>
        /// Convert the FetchXML query object to a string
        /// </summary>
        /// <param name="fetch">The FetchXML query object to convert</param>
        /// <returns>The string representation of the query</returns>
        internal static string Serialize(FetchType fetch)
        {
            var serializer = new XmlSerializer(typeof(FetchType));

            using (var writer = new StringWriter())
            using (var xmlWriter = System.Xml.XmlWriter.Create(writer, new System.Xml.XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true
            }))
            {
                // Add in a separate namespace to remove the xsi and xsd namespaces added by default
                var xsn = new XmlSerializerNamespaces();
                xsn.Add("generator", "MarkMpn.SQL4CDS");

                serializer.Serialize(xmlWriter, fetch, xsn);
                return writer.ToString();
            }
        }

        protected string GetDisplayName(int count, EntityMetadata meta)
        {
            if (count == 1)
                return meta.DisplayName?.UserLocalizedLabel?.Label ?? meta.LogicalName;

            return meta.DisplayCollectionName?.UserLocalizedLabel?.Label ??
                meta.LogicalCollectionName ??
                meta.LogicalName;
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            return Array.Empty<IExecutionPlanNode>();
        }

        public override NodeSchema GetSchema(IAttributeMetadataCache metadata)
        {
            var schema = new NodeSchema();

            // Add each attribute from the main entity and recurse into link entities
            var entity = FetchXml.Items.OfType<FetchEntityType>().Single();
            var meta = metadata[entity.name];
            schema.PrimaryKey = $"{Alias}.{meta.PrimaryIdAttribute}";
            AddAttributes(schema, metadata, entity.name, Alias, entity.Items);
            
            return schema;
        }

        private void AddAttributes(NodeSchema schema, IAttributeMetadataCache metadata, string entityName, string alias, object[] items)
        {
            if (items == null && !ReturnFullSchema)
                return;

            var meta = metadata[entityName];

            if (ReturnFullSchema)
            {
                foreach (var attrMetadata in meta.Attributes)
                {
                    var attrType = GetAttributeType(attrMetadata);
                    var fullName = $"{alias}.{attrMetadata.LogicalName}";

                    schema.Schema[fullName] = attrType;

                    if (!schema.Aliases.TryGetValue(attrMetadata.LogicalName, out var simpleColumnNameAliases))
                    {
                        simpleColumnNameAliases = new List<string>();
                        schema.Aliases[attrMetadata.LogicalName] = simpleColumnNameAliases;
                    }

                    simpleColumnNameAliases.Add(fullName);
                }
            }
            else
            {
                foreach (var attribute in items.OfType<FetchAttributeType>())
                {
                    var attrMetadata = meta.Attributes.Single(a => a.LogicalName == attribute.name);
                    var attrType = GetAttributeType(attrMetadata);

                    if (attribute.aggregateSpecified && (attribute.aggregate == AggregateType.count || attribute.aggregate == AggregateType.countcolumn))
                        attrType = typeof(int);

                    var attrName = attribute.alias ?? attribute.name;
                    var fullName = $"{alias}.{attrName}";

                    schema.Schema[fullName] = attrType;

                    if (!schema.Aliases.TryGetValue(attrName, out var simpleColumnNameAliases))
                    {
                        simpleColumnNameAliases = new List<string>();
                        schema.Aliases[attrName] = simpleColumnNameAliases;
                    }

                    simpleColumnNameAliases.Add(fullName);
                }
            }

            if (items != null)
            {
                foreach (var linkEntity in items.OfType<FetchLinkEntityType>())
                {
                    if (schema.PrimaryKey != null)
                    {
                        var childMeta = metadata[linkEntity.name];

                        if (linkEntity.from != childMeta.PrimaryIdAttribute)
                        {
                            if (linkEntity.linktype == "inner")
                                schema.PrimaryKey = $"{linkEntity.alias}.{childMeta.PrimaryIdAttribute}";
                            else
                                schema.PrimaryKey = null;
                        }
                    }

                    AddAttributes(schema, metadata, linkEntity.name, linkEntity.alias, linkEntity.Items);
                }
            }
        }

        private Type GetAttributeType(AttributeMetadata attrMetadata)
        {
            return typeof(string);
        }

        public override IEnumerable<string> GetRequiredColumns()
        {
            return Array.Empty<string>();
        }
    }
}