﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace MarkMpn.Sql4Cds.Engine.Visitors
{
    class InSubqueryVisitor : TSqlFragmentVisitor
    {
        public List<InPredicate> InSubqueries { get; } = new List<InPredicate>();

        public override void ExplicitVisit(InPredicate node)
        {
            if (node.Subquery != null)
                InSubqueries.Add(node);
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            // Do not recurse into subqueries
        }

        public override void ExplicitVisit(FromClause node)
        {
            // Do not recurse into data sources
        }
    }
}
