﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MarkMpn.Sql4Cds.Engine.FetchXml;
using MarkMpn.Sql4Cds.Engine.Visitors;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    /// <summary>
    /// A base class for execution plan nodes that generate a stream of data
    /// </summary>
    abstract class BaseDataNode : BaseNode, IDataExecutionPlanNode
    {
        private int _executionCount;
        private int _tickCount;
        private int _rowsOut;

        /// <summary>
        /// Executes the query and produces a stram of data in the results
        /// </summary>
        /// <param name="org">The <see cref="IOrganizationService"/> to use to get the data</param>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExpressionVisitor"/> to indicate how the query can be executed</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <param name="parameterValues">A mapping of parameter names to their current values</param>
        /// <returns>A stream of <see cref="Entity"/> records</returns>
        public IEnumerable<Entity> Execute(IOrganizationService org, IAttributeMetadataCache metadata, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes, IDictionary<string, object> parameterValues)
        {
            // Track execution times roughly using Environment.TickCount. Stopwatch provides more accurate results
            // but gives a large performance penalty.
            IEnumerator<Entity> enumerator;

            var start = Environment.TickCount;
            try
            {
                _executionCount++;

                enumerator = ExecuteInternal(org, metadata, options, parameterTypes, parameterValues).GetEnumerator();
            }
            catch (QueryExecutionException ex)
            {
                if (ex.Node == null)
                    ex.Node = this;

                throw;
            }
            catch (Exception ex)
            {
                throw new QueryExecutionException(ex.Message, ex) { Node = this };
            }
            finally
            {
                var end = Environment.TickCount;
                _tickCount += (end - start);
            }

            while (!options.Cancelled)
            {
                Entity current;

                try
                {
                    start = Environment.TickCount;
                    if (!enumerator.MoveNext())
                        break;

                    current = enumerator.Current;
                }
                catch (QueryExecutionException ex)
                {
                    if (ex.Node == null)
                        ex.Node = this;

                    throw;
                }
                catch (Exception ex)
                {
                    throw new QueryExecutionException(ex.Message, ex) { Node = this };
                }
                finally
                {
                    var end = Environment.TickCount;
                    _tickCount += (end - start);
                }

                _rowsOut++;
                yield return current;
            }
        }

        /// <summary>
        /// Estimates the number of rows that will be returned by the node
        /// </summary>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <param name="tableSize">A cache of the number of records in each table</param>
        /// <returns>The number of rows the node is estimated to return</returns>
        public abstract int EstimateRowsOut(IAttributeMetadataCache metadata, IDictionary<string, Type> parameterTypes, ITableSizeCache tableSize);

        /// <summary>
        /// Returns the number of times the node has been executed
        /// </summary>
        public override int ExecutionCount => _executionCount;

        /// <summary>
        /// Returns the time that the node has taken to execute
        /// </summary>
        public override TimeSpan Duration => TimeSpan.FromMilliseconds(_tickCount);

        /// <summary>
        /// Returns the number of rows that the node has generated
        /// </summary>
        [Category("Statistics")]
        [Description("Returns the number of rows that the node has generated")]
        public int RowsOut => _rowsOut;

        /// <summary>
        /// Produces the data for the node without keeping track of any execution time statistics
        /// </summary>
        /// <param name="org">The <see cref="IOrganizationService"/> to use to get the data</param>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExpressionVisitor"/> to indicate how the query can be executed</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <param name="parameterValues">A mapping of parameter names to their current values</param>
        /// <returns>A stream of <see cref="Entity"/> records</returns>
        protected abstract IEnumerable<Entity> ExecuteInternal(IOrganizationService org, IAttributeMetadataCache metadata, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes, IDictionary<string, object> parameterValues);

        /// <summary>
        /// Gets the details of columns produced by the node
        /// </summary>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <returns>Details of the columns produced by the node</returns>
        public abstract NodeSchema GetSchema(IAttributeMetadataCache metadata, IDictionary<string, Type> parameterTypes);

        /// <summary>
        /// Attempts to fold this node into its source to simplify the query
        /// </summary>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExpressionVisitor"/> to indicate how the query can be executed</param>
        /// <param name="parameterTypes">A mapping of parameter names to their related types</param>
        /// <returns>The node that should be used in place of this node</returns>
        public abstract IDataExecutionPlanNode FoldQuery(IAttributeMetadataCache metadata, IQueryExecutionOptions options, IDictionary<string, Type> parameterTypes);

        /// <summary>
        /// Translates filter criteria from ScriptDom to FetchXML
        /// </summary>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExpressionVisitor"/> to indicate how the query can be executed</param>
        /// <param name="criteria">The SQL criteria to attempt to translate to FetchXML</param>
        /// <param name="schema">The schema of the node that the criteria apply to</param>
        /// <param name="allowedPrefix">The prefix of the table that the <paramref name="criteria"/> can be translated for, or <c>null</c> if any tables can be referenced</param>
        /// <param name="targetEntityName">The logical name of the root entity that the FetchXML query is targetting</param>
        /// <param name="targetEntityAlias">The alias of the root entity that the FetchXML query is targetting</param>
        /// <param name="items">The child items of the root entity in the FetchXML query</param>
        /// <param name="filter">The FetchXML version of the <paramref name="criteria"/> that is generated by this method</param>
        /// <returns><c>true</c> if the <paramref name="criteria"/> can be translated to FetchXML, or <c>false</c> otherwise</returns>
        protected bool TranslateFetchXMLCriteria(IAttributeMetadataCache metadata, IQueryExecutionOptions options, BooleanExpression criteria, NodeSchema schema, string allowedPrefix, string targetEntityName, string targetEntityAlias, object[] items, out filter filter)
        {
            if (!TranslateFetchXMLCriteria(metadata, options, criteria, schema, allowedPrefix, targetEntityName, targetEntityAlias, items, out var condition, out filter))
                return false;

            if (condition != null)
                filter = new filter { Items = new object[] { condition } };

            return true;
        }

        /// <summary>
        /// Translates filter criteria from ScriptDom to FetchXML
        /// </summary>
        /// <param name="metadata">The <see cref="IAttributeMetadataCache"/> to use to get metadata</param>
        /// <param name="options"><see cref="IQueryExpressionVisitor"/> to indicate how the query can be executed</param>
        /// <param name="criteria">The SQL criteria to attempt to translate to FetchXML</param>
        /// <param name="schema">The schema of the node that the criteria apply to</param>
        /// <param name="allowedPrefix">The prefix of the table that the <paramref name="criteria"/> can be translated for, or <c>null</c> if any tables can be referenced</param>
        /// <param name="targetEntityName">The logical name of the root entity that the FetchXML query is targetting</param>
        /// <param name="targetEntityAlias">The alias of the root entity that the FetchXML query is targetting</param>
        /// <param name="items">The child items of the root entity in the FetchXML query</param>
        /// <param name="filter">The FetchXML version of the <paramref name="criteria"/> that is generated by this method when it covers multiple conditions</param>
        /// <param name="condition">The FetchXML version of the <paramref name="criteria"/> that is generated by this method when it is for a single condition only</param>
        /// <returns><c>true</c> if the <paramref name="criteria"/> can be translated to FetchXML, or <c>false</c> otherwise</returns>
        private bool TranslateFetchXMLCriteria(IAttributeMetadataCache metadata, IQueryExecutionOptions options, BooleanExpression criteria, NodeSchema schema, string allowedPrefix, string targetEntityName, string targetEntityAlias, object[] items, out condition condition, out filter filter)
        {
            condition = null;
            filter = null;

            if (criteria is BooleanBinaryExpression binary)
            {
                if (!TranslateFetchXMLCriteria(metadata, options, binary.FirstExpression, schema, allowedPrefix, targetEntityName, targetEntityAlias, items, out var lhsCondition, out var lhsFilter))
                    return false;
                if (!TranslateFetchXMLCriteria(metadata, options, binary.SecondExpression, schema, allowedPrefix, targetEntityName, targetEntityAlias, items, out var rhsCondition, out var rhsFilter))
                    return false;

                filter = new filter
                {
                    type = binary.BinaryExpressionType == BooleanBinaryExpressionType.And ? filterType.and : filterType.or,
                    Items = new[]
                    {
                        (object) lhsCondition ?? lhsFilter,
                        (object) rhsCondition ?? rhsFilter
                    }
                };
                return true;
            }

            if (criteria is BooleanComparisonExpression comparison)
            {
                // Handle most comparison operators (=, <> etc.)
                // Comparison can be between a column and either a literal value, function call or another column (for joins only)
                // Function calls are used to represent more complex FetchXML query operators
                // Operands could be in either order, so `column = 'value'` or `'value' = column` should both be allowed
                var field = comparison.FirstExpression as ColumnReferenceExpression;
                var literal = comparison.SecondExpression as Literal;
                var func = comparison.SecondExpression as FunctionCall;
                var field2 = comparison.SecondExpression as ColumnReferenceExpression;
                var variable = comparison.SecondExpression as VariableReference;
                var expr = comparison.SecondExpression;
                var type = comparison.ComparisonType;

                if (field != null && field2 != null)
                {
                    // The operator is comparing two attributes. This is allowed in join criteria,
                    // but not in filter conditions before version 9.1.0.19251
                    if (!options.ColumnComparisonAvailable)
                        return false;
                }

                // If we couldn't find the pattern `column = value` or `column = func()`, try looking in the opposite order
                if (field == null && literal == null && func == null && variable == null)
                {
                    field = comparison.SecondExpression as ColumnReferenceExpression;
                    literal = comparison.FirstExpression as Literal;
                    func = comparison.FirstExpression as FunctionCall;
                    variable = comparison.FirstExpression as VariableReference;
                    expr = comparison.FirstExpression;
                    field2 = null;

                    // Switch the operator depending on the order of the column and value, so `column > 3` uses gt but `3 > column` uses le
                    switch (type)
                    {
                        case BooleanComparisonType.GreaterThan:
                            type = BooleanComparisonType.LessThan;
                            break;

                        case BooleanComparisonType.GreaterThanOrEqualTo:
                            type = BooleanComparisonType.LessThanOrEqualTo;
                            break;

                        case BooleanComparisonType.LessThan:
                            type = BooleanComparisonType.GreaterThan;
                            break;

                        case BooleanComparisonType.LessThanOrEqualTo:
                            type = BooleanComparisonType.GreaterThanOrEqualTo;
                            break;
                    }
                }

                // If we still couldn't find the column name and value, this isn't a pattern we can support in FetchXML
                if (field == null || (literal == null && func == null && variable == null && (field2 == null || !options.ColumnComparisonAvailable) && !expr.IsConstantValueExpression(schema, out literal)))
                    return false;

                // Select the correct FetchXML operator
                @operator op;

                switch (type)
                {
                    case BooleanComparisonType.Equals:
                        op = @operator.eq;
                        break;

                    case BooleanComparisonType.GreaterThan:
                        op = @operator.gt;
                        break;

                    case BooleanComparisonType.GreaterThanOrEqualTo:
                        op = @operator.ge;
                        break;

                    case BooleanComparisonType.LessThan:
                        op = @operator.lt;
                        break;

                    case BooleanComparisonType.LessThanOrEqualTo:
                        op = @operator.le;
                        break;

                    case BooleanComparisonType.NotEqualToBrackets:
                    case BooleanComparisonType.NotEqualToExclamation:
                        op = @operator.ne;
                        break;

                    default:
                        throw new NotSupportedQueryFragmentException("Unsupported comparison type", comparison);
                }

                string value = null;
                List<string> values = null;

                if (literal != null)
                {
                    value = literal.Value;
                }
                else if (variable != null)
                {
                    value = variable.Name;
                }
                else if (func != null && Enum.TryParse<@operator>(func.FunctionName.Value.ToLower(), out var customOperator))
                {
                    if (op == @operator.eq)
                    {
                        // If we've got the pattern `column = func()`, select the FetchXML operator from the function name
                        op = customOperator;

                        // Check for unsupported SQL DOM elements within the function call
                        if (func.CallTarget != null)
                            throw new NotSupportedQueryFragmentException("Unsupported function call target", func);

                        if (func.Collation != null)
                            throw new NotSupportedQueryFragmentException("Unsupported function collation", func);

                        if (func.OverClause != null)
                            throw new NotSupportedQueryFragmentException("Unsupported function OVER clause", func);

                        if (func.UniqueRowFilter != UniqueRowFilter.NotSpecified)
                            throw new NotSupportedQueryFragmentException("Unsupported function unique filter", func);

                        if (func.WithinGroupClause != null)
                            throw new NotSupportedQueryFragmentException("Unsupported function group clause", func);

                        if (func.Parameters.Count > 1 && op != @operator.containvalues && op != @operator.notcontainvalues)
                            throw new NotSupportedQueryFragmentException("Unsupported number of function parameters", func);

                        // Some advanced FetchXML operators use a value as well - take this as the function parameter
                        // This provides support for queries such as `createdon = lastxdays(3)` becoming <condition attribute="createdon" operator="last-x-days" value="3" />
                        if (op == @operator.containvalues || op == @operator.notcontainvalues ||
                            ((op == @operator.infiscalperiodandyear || op == @operator.inorafterfiscalperiodandyear || op == @operator.inorbeforefiscalperiodandyear) && func.Parameters.Count == 2))
                        {
                            values = new List<string>();

                            foreach (var funcParam in func.Parameters)
                            {
                                if (!(funcParam is Literal paramLiteral))
                                    throw new NotSupportedQueryFragmentException("Unsupported function parameter", funcParam);

                                values.Add(paramLiteral.Value);
                            }
                        }
                        else if (func.Parameters.Count == 1)
                        {
                            if (!(func.Parameters[0] is Literal paramLiteral))
                                throw new NotSupportedQueryFragmentException("Unsupported function parameter", func.Parameters[0]);

                            value = paramLiteral.Value;
                        }
                    }
                    else
                    {
                        // Can't use functions with other operators
                        throw new NotSupportedQueryFragmentException("Unsupported function use. Only <field> = <func>(<param>) usage is supported", comparison);
                    }
                }
                else if (func != null)
                {
                    if (func.IsConstantValueExpression(schema, out literal))
                        value = literal.Value;
                    else
                        throw new PostProcessingRequiredException("Unsupported FetchXML function", func);
                }

                // Find the entity that the condition applies to, which may be different to the entity that the condition FetchXML element will be 
                // added within
                var columnName = field.GetColumnName();
                if (!schema.ContainsColumn(columnName, out columnName))
                    return false;

                var parts = columnName.Split('.');

                if (parts.Length != 2)
                    return false;

                var entityAlias = parts[0];
                var attrName = parts[1];

                if (allowedPrefix != null && !allowedPrefix.Equals(entityAlias))
                    return false;

                var entityName = AliasToEntityName(targetEntityAlias, targetEntityName, items, entityAlias);

                var meta = metadata[entityName];

                if (field2 == null)
                {
                    var attribute = meta.Attributes.SingleOrDefault(a => a.LogicalName.Equals(attrName, StringComparison.OrdinalIgnoreCase));

                    if (!String.IsNullOrEmpty(attribute?.AttributeOf) && meta.Attributes.Any(a => a.LogicalName == attribute.AttributeOf))
                    {
                        var baseAttribute = meta.Attributes.Single(a => a.LogicalName == attribute.AttributeOf);
                        var virtualAttributeHandled = false;

                        // If filtering on the display name of an optionset attribute, convert it to filtering on the underlying value field
                        // instead where possible.
                        if (attribute.LogicalName == baseAttribute.LogicalName + "name" && baseAttribute is EnumAttributeMetadata enumAttr)
                        {
                            var matchingOptions = enumAttr.OptionSet.Options.Where(o => o.Label.UserLocalizedLabel.Label.Equals(value, StringComparison.OrdinalIgnoreCase)).ToList();

                            if (matchingOptions.Count == 1)
                            {
                                attrName = baseAttribute.LogicalName;
                                value = matchingOptions[0].Value.ToString();
                                virtualAttributeHandled = true;
                            }
                            else if (matchingOptions.Count == 0 && (op == @operator.eq || op == @operator.ne || op == @operator.neq))
                            {
                                throw new NotSupportedQueryFragmentException("Unknown optionset value. Supported values are " + String.Join(", ", enumAttr.OptionSet.Options.Select(o => o.Label.UserLocalizedLabel.Label)), literal);
                            }
                        }

                        // If filtering on the display name of a lookup value, add a join to the target type and filter
                        // on the primary name attribute instead.
                        if (attribute.LogicalName == baseAttribute.LogicalName + "name" && baseAttribute is LookupAttributeMetadata lookupAttr && lookupAttr.Targets.Length == 1)
                        {
                            // TODO:
                            return false;
                            /*
                            var targetMetadata = Metadata[lookupAttr.Targets[0]];
                            var join = entityTable.GetItems().OfType<FetchLinkEntityType>().FirstOrDefault(link => link.name == targetMetadata.LogicalName && link.from == targetMetadata.PrimaryIdAttribute && link.to == baseAttribute.LogicalName && link.linktype == "outer");

                            if (join == null)
                            {
                                join = new FetchLinkEntityType
                                {
                                    name = targetMetadata.LogicalName,
                                    from = targetMetadata.PrimaryIdAttribute,
                                    to = baseAttribute.LogicalName,
                                    alias = $"{entityTable.EntityName}_{baseAttribute.LogicalName}",
                                    linktype = "outer"
                                };
                                var joinTable = new EntityTable(Metadata, join) { Hidden = true };
                                tables.Add(joinTable);

                                entityTable.AddItem(join);
                                entityTable = joinTable;
                            }
                            else
                            {
                                entityTable = tables.Single(t => t.LinkEntity == join);
                            }

                            entityName = entityTable.Alias;
                            attrName = targetMetadata.PrimaryNameAttribute;
                            virtualAttributeHandled = true;
                            */
                        }

                        if (!virtualAttributeHandled)
                            return false;
                    }

                    if (!Int32.TryParse(value, out _) && attribute?.AttributeType == AttributeTypeCode.EntityName)
                    {
                        // Convert the entity name to the object type code
                        var targetMetadata = metadata[value];

                        value = targetMetadata.ObjectTypeCode?.ToString();
                    }

                    condition = new condition
                    {
                        entityname = StandardizeAlias(entityAlias, targetEntityAlias, items),
                        attribute = attrName.ToLowerInvariant(),
                        @operator = op,
                        value = value
                    };
                    return true;
                }
                else
                {
                    // Column comparisons can only happen within a single entity
                    var columnName2 = field2.GetColumnName();
                    if (!schema.ContainsColumn(columnName2, out columnName2))
                        return false;

                    var parts2 = columnName2.Split('.');
                    var entityAlias2 = parts2[0];
                    var attrName2 = parts2[1];

                    if (!entityAlias.Equals(entityAlias2, StringComparison.OrdinalIgnoreCase))
                        return false;

                    var attr1 = meta.Attributes.SingleOrDefault(a => a.LogicalName.Equals(attrName, StringComparison.OrdinalIgnoreCase));
                    var attr2 = meta.Attributes.SingleOrDefault(a => a.LogicalName.Equals(attrName2, StringComparison.OrdinalIgnoreCase));

                    if (!String.IsNullOrEmpty(attr1?.AttributeOf))
                        return false;

                    if (!String.IsNullOrEmpty(attr2?.AttributeOf))
                        return false;

                    condition = new condition
                    {
                        entityname = StandardizeAlias(entityAlias, targetEntityAlias, items),
                        attribute = attrName.ToLowerInvariant(),
                        @operator = op,
                        valueof = attrName2.ToLowerInvariant()
                    };
                    return true;
                }
            }

            if (criteria is InPredicate inPred)
            {
                // Checking if a column is in a list of literals is foldable, everything else isn't
                if (!(inPred.Expression is ColumnReferenceExpression inCol))
                    return false;

                if (inPred.Subquery != null)
                    return false;

                if (!inPred.Values.All(v => v is Literal))
                    return false;

                var columnName = inCol.GetColumnName();

                if (!schema.ContainsColumn(columnName, out columnName))
                    return false;

                var parts = columnName.Split('.');
                var entityAlias = parts[0];
                var attrName = parts[1];

                if (allowedPrefix != null && !allowedPrefix.Equals(entityAlias))
                    return false;

                condition = new condition
                {
                    entityname = StandardizeAlias(entityAlias, targetEntityAlias, items),
                    attribute = attrName.ToLowerInvariant(),
                    @operator = inPred.NotDefined ? @operator.notin : @operator.@in,
                    Items = inPred.Values.Cast<Literal>().Select(lit => new conditionValue { Value = lit.Value }).ToArray()
                };
                return true;
            }

            if (criteria is BooleanIsNullExpression isNull)
            {
                if (!(isNull.Expression is ColumnReferenceExpression nullCol))
                    return false;

                var columnName = nullCol.GetColumnName();

                if (!schema.ContainsColumn(columnName, out columnName))
                    return false;

                var parts = columnName.Split('.');
                var entityAlias = parts[0];
                var attrName = parts[1];

                if (allowedPrefix != null && !allowedPrefix.Equals(entityAlias))
                    return false;

                condition = new condition
                {
                    entityname = StandardizeAlias(entityAlias, targetEntityAlias, items),
                    attribute = attrName.ToLowerInvariant(),
                    @operator = isNull.IsNot ? @operator.notnull : @operator.@null
                };
                return true;
            }

            if (criteria is LikePredicate like)
            {
                if (!(like.FirstExpression is ColumnReferenceExpression col))
                    return false;

                if (!(like.SecondExpression is StringLiteral value))
                    return false;

                if (like.EscapeExpression != null)
                    return false;

                var columnName = col.GetColumnName();

                if (!schema.ContainsColumn(columnName, out columnName))
                    return false;

                var parts = columnName.Split('.');
                var entityAlias = parts[0];
                var attrName = parts[1];

                if (allowedPrefix != null && !allowedPrefix.Equals(entityAlias))
                    return false;

                condition = new condition
                {
                    entityname = StandardizeAlias(entityAlias, targetEntityAlias, items),
                    attribute = attrName.ToLowerInvariant(),
                    @operator = like.NotDefined ? @operator.notlike : @operator.like,
                    value = value.Value
                };
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the alias to use for an entity or link-entity in the entityname property of a FetchXML condition
        /// </summary>
        /// <param name="entityAlias">The alias of the table that the condition refers to</param>
        /// <param name="targetEntityAlias">The alias of the root entity in the FetchXML query</param>
        /// <param name="items">The child items in the root entity object</param>
        /// <returns>The entityname to use in the FetchXML condition</returns>
        private string StandardizeAlias(string entityAlias, string targetEntityAlias, object[] items)
        {
            if (entityAlias.Equals(targetEntityAlias, StringComparison.OrdinalIgnoreCase))
                return null;

            var entity = new FetchEntityType { Items = items };
            var linkEntity = entity.FindLinkEntity(entityAlias);

            return linkEntity.alias;
        }

        /// <summary>
        /// Gets the logical name of an entity from the alias of a table
        /// </summary>
        /// <param name="targetEntityAlias">The alias of the root entity in the FetchXML query</param>
        /// <param name="targetEntityName">The logical name of the root entity in the FetchXML query</param>
        /// <param name="items">The child items in the root entity object</param>
        /// <param name="alias">The alias of the table to get the logical name for</param>
        /// <returns>The logical name of the aliased entity</returns>
        private string AliasToEntityName(string targetEntityAlias, string targetEntityName, object[] items, string alias)
        {
            if (targetEntityAlias.Equals(alias, StringComparison.OrdinalIgnoreCase))
                return targetEntityName;

            var entity = new FetchEntityType { Items = items };
            var linkEntity = entity.FindLinkEntity(alias);

            return linkEntity.name;
        }
    }
}
