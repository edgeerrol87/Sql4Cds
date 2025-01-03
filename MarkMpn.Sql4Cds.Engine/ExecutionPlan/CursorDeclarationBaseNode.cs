﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    abstract class CursorDeclarationBaseNode : BaseNode, IDmlQueryExecutionPlanNode
    {
        public bool _isOpen;

        public string Sql { get; set; }
        public int Index { get; set; }
        public int Length { get; set; }
        public int LineNumber { get; set; }
        public override int ExecutionCount => 0;

        public override TimeSpan Duration => TimeSpan.Zero;

        public string CursorName { get; set; }

        public CursorOptionKind Scope { get; set; }

        public IDmlQueryExecutionPlanNode PopulationQuery { get; set; }

        public IDataReaderExecutionPlanNode FetchQuery { get; set; }

        public override void AddRequiredColumns(NodeCompilationContext context, IList<string> requiredColumns)
        {
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            if (PopulationQuery != null)
                yield return PopulationQuery;

            if (FetchQuery != null)
                yield return FetchQuery;
        }

        public abstract object Clone();

        public IRootExecutionPlanNodeInternal[] FoldQuery(NodeCompilationContext context, IList<OptimizerHint> hints)
        {
            if (PopulationQuery != null)
                PopulationQuery = (IDmlQueryExecutionPlanNode)PopulationQuery.FoldQuery(context, hints).Single();

            if (FetchQuery is IDataExecutionPlanNodeInternal fetch)
                FetchQuery = (IDataReaderExecutionPlanNode)fetch.FoldQuery(context, hints);

            return new[] { this };
        }

        public void Execute(NodeExecutionContext context, out int recordsAffected, out string message)
        {
            // Make sure a cursor with this name doesn't already exist in the selected scope
            IDictionary<string, CursorDeclarationBaseNode> cursors;

            if (Scope == CursorOptionKind.Global)
                cursors = context.Session.Cursors;
            else
                cursors = context.Cursors;

            if (cursors.ContainsKey(CursorName))
                throw new QueryExecutionException(Sql4CdsError.DuplicateCursorName(CursorName));

            // Store a copy of this cursor in the session so it can be referenced by later FETCH statements
            cursors[CursorName] = (CursorDeclarationBaseNode)Clone();

            // Remove the population and fetch queries from this clone so they doesn't appear in the execution plan
            recordsAffected = -1;
            message = null;

            PopulationQuery = null;
            FetchQuery = null;
        }

        public virtual IDmlQueryExecutionPlanNode Open(NodeExecutionContext context)
        {
            if (_isOpen)
                throw new QueryExecutionException("Cursor is already open");

            _isOpen = true;
            return PopulationQuery;
        }

        public virtual void Close(NodeExecutionContext context)
        {
            if (!_isOpen)
                throw new QueryExecutionException("Cursor is already closed");

            _isOpen = false;
        }

        public virtual IDataReaderExecutionPlanNode Fetch(NodeExecutionContext context)
        {
            if (!_isOpen)
                throw new QueryExecutionException("Cursor is closed");

            return FetchQuery;
        }
    }
}
