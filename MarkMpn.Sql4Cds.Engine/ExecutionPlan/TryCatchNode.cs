﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using Microsoft.Xrm.Sdk;

namespace MarkMpn.Sql4Cds.Engine.ExecutionPlan
{
    /// <summary>
    /// Allows execution of a different query plan if the first one fails
    /// </summary>
    class TryCatchNode : BaseDataNode
    {
        [Browsable(false)]
        public IDataExecutionPlanNodeInternal TrySource { get; set; }

        [Browsable(false)]
        public IDataExecutionPlanNodeInternal CatchSource { get; set; }

        [Browsable(false)]
        public Func<Exception,bool> ExceptionFilter { get; set; }

        /// <summary>
        /// The text of the exception that was handled by this node
        /// </summary>
        [Category("Try/Catch")]
        [DisplayName("Caught Error")]
        [Description("The error generated by the Try branch that caused execution to move to the Catch branch")]
        public string CaughtException { get; set; }

        protected override IEnumerable<Entity> ExecuteInternal(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, DataTypeReference> parameterTypes, IDictionary<string, object> parameterValues)
        {
            var useCatchSource = false;
            IEnumerator<Entity> enumerator;

            try
            {
                enumerator = TrySource.Execute(dataSources, options, parameterTypes, parameterValues).GetEnumerator();
            }
            catch (Exception ex)
            {
                if (ExceptionFilter != null && !ExceptionFilter(ex))
                    throw;

                useCatchSource = true;
                enumerator = null;
                CaughtException = ex.Message;
            }

            var doneFirst = false;

            while (!useCatchSource && !options.CancellationToken.IsCancellationRequested)
            {
                Entity current;

                try
                {
                    if (!enumerator.MoveNext())
                        break;

                    doneFirst = true;
                    current = enumerator.Current;
                }
                catch (Exception ex)
                {
                    if (doneFirst || ExceptionFilter != null && !ExceptionFilter(ex))
                        throw;

                    useCatchSource = true;
                    current = null;
                    CaughtException = ex.Message;
                }
                
                if (!useCatchSource)
                    yield return current;
            }

            if (useCatchSource)
            {
                foreach (var entity in CatchSource.Execute(dataSources, options, parameterTypes, parameterValues))
                    yield return entity;
            }
        }

        public override INodeSchema GetSchema(IDictionary<string, DataSource> dataSources, IDictionary<string, DataTypeReference> parameterTypes)
        {
            var trySchema = TrySource.GetSchema(dataSources, parameterTypes);
            var catchSchema = CatchSource.GetSchema(dataSources, parameterTypes);

            // Columns should be the same but sort order may be different
            if (trySchema.SortOrder.SequenceEqual(catchSchema.SortOrder, StringComparer.OrdinalIgnoreCase))
                return trySchema;

            var consistentSorts = trySchema.SortOrder
                .TakeWhile((sort, index) => index < catchSchema.SortOrder.Count && sort.Equals(catchSchema.SortOrder[index], StringComparison.OrdinalIgnoreCase))
                .ToList();

            return new NodeSchema(trySchema) { SortOrder = consistentSorts };
        }

        public override IEnumerable<IExecutionPlanNode> GetSources()
        {
            yield return TrySource;
            yield return CatchSource;
        }

        public override IDataExecutionPlanNodeInternal FoldQuery(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, DataTypeReference> parameterTypes, IList<OptimizerHint> hints)
        {
            TrySource = TrySource.FoldQuery(dataSources, options, parameterTypes, hints);
            TrySource.Parent = this;
            CatchSource = CatchSource.FoldQuery(dataSources, options, parameterTypes, hints);
            CatchSource.Parent = this;
            return this;
        }

        public override void AddRequiredColumns(IDictionary<string, DataSource> dataSources, IDictionary<string, DataTypeReference> parameterTypes, IList<string> requiredColumns)
        {
            TrySource.AddRequiredColumns(dataSources, parameterTypes, requiredColumns);
            CatchSource.AddRequiredColumns(dataSources, parameterTypes, requiredColumns);
        }

        protected override RowCountEstimate EstimateRowsOutInternal(IDictionary<string, DataSource> dataSources, IQueryExecutionOptions options, IDictionary<string, DataTypeReference> parameterTypes)
        {
            return TrySource.EstimateRowsOut(dataSources, options, parameterTypes);
        }

        public override object Clone()
        {
            var clone = new TryCatchNode
            {
                TrySource = (IDataExecutionPlanNodeInternal)TrySource.Clone(),
                CatchSource = (IDataExecutionPlanNodeInternal)CatchSource.Clone(),
                ExceptionFilter = ExceptionFilter
            };

            clone.TrySource.Parent = clone;
            clone.CatchSource.Parent = clone;

            return clone;
        }
    }
}
