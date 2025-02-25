﻿using MarkMpn.Sql4Cds.Engine.ExecutionPlan;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using System;
using System.Collections.Generic;
using System.Linq;
using Wmhelp.XPath2.AST;

namespace MarkMpn.Sql4Cds.Engine.Visitors
{
    /// <summary>
    /// Replaces expressions with the equivalent column names
    /// </summary>
    /// <remarks>
    /// During the post-processing of aggregate queries, a new schema is produced where the aggregates are stored in new
    /// columns. To make the processing of the remainder of the query easier, this class replaces any references to those
    /// aggregate functions with references to the calculated column name, e.g.
    /// SELECT firstname, count(*) FROM contact HAVING count(*) > 2
    /// would become
    /// SELECT firstname, agg1 FROM contact HAVING agg1 > 2
    /// 
    /// During query execution the agg1 column is generated from the aggregate query and allows the rest of the query execution
    /// to proceed without knowledge of how the aggregate was derived.
    /// </remarks>
    class RewriteVisitor : RewriteVisitorBase
    {
        private readonly Dictionary<string, ScalarExpression> _mappings;
        private readonly Dictionary<string, string> _xpathMappings;

        public RewriteVisitor(IDictionary<ScalarExpression,string> rewrites)
        {
            _mappings = rewrites
                .GroupBy(kvp => kvp.Key.ToNormalizedSql(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => (ScalarExpression) g.First().Value.ToColumnReference(),
                    StringComparer.OrdinalIgnoreCase);

            _xpathMappings = rewrites
                .Where(kvp => kvp.Key is ColumnReferenceExpression)
                .GroupBy(kvp => kvp.Key.ToNormalizedSql(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => $"sql:column(\"{g.Key}\")",
                    g => $"sql:column(\"{g.First().Value}\")",
                    StringComparer.OrdinalIgnoreCase);
        }

        public RewriteVisitor(IDictionary<ScalarExpression,ScalarExpression> rewrites)
        {
            _mappings = rewrites
                .GroupBy(kvp => kvp.Key.ToNormalizedSql(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().Value,
                    StringComparer.OrdinalIgnoreCase);

            _xpathMappings = rewrites
                .Where(kvp => kvp.Key is ColumnReferenceExpression && (kvp.Value is ColumnReferenceExpression || kvp.Value is VariableReference))
                .GroupBy(kvp => kvp.Key.ToNormalizedSql(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => $"sql:column(\"{g.Key}\")",
                    g => g.First().Value is ColumnReferenceExpression col
                        ? $"sql:column(\"{col.GetColumnName()}\")"
                        : $"sql:variable(\"{((VariableReference)g.First().Value).Name}\")",
                    StringComparer.OrdinalIgnoreCase);
        }

        protected override ScalarExpression ReplaceExpression(ScalarExpression expression, out string name)
        {
            name = null;

            if (expression == null)
                return null;

            if (_mappings.TryGetValue(expression.ToNormalizedSql(), out var column))
            {
                name = (column as ColumnReferenceExpression)?.MultiPartIdentifier?.Identifiers?.Last()?.Value;
                return column;
            }

            return expression;
        }

        protected override BooleanExpression ReplaceExpression(BooleanExpression expression)
        {
            return expression;
        }

        public override void ExplicitVisit(FunctionCall node)
        {
            base.ExplicitVisit(node);

            if (node.CallTarget == null ||
                node.Parameters.Count != 2 ||
                !(node.Parameters[0] is StringLiteral literal) ||
                _xpathMappings.Count == 0)
                return;

            foreach (var mapping in _xpathMappings)
                literal.Value = literal.Value.Replace(mapping.Key, mapping.Value);
        }
    }
}
