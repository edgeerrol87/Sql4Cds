﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarkMpn.Sql4Cds.XTB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkMpn.Sql4Cds.Tests
{
    [TestClass]
    public class FormatterTests
    {
        [TestMethod]
        public void SimpleSelect()
        {
            var original = @"select * from tbl";
            var expected = @"SELECT *
FROM   tbl;";
            Assert.AreEqual(expected, Formatter.Format(original));
        }

        [TestMethod]
        public void SingleLineLeadingComment()
        {
            var original = @"-- comment here
select * from tbl";
            var expected = @"-- comment here
SELECT *
FROM   tbl;";
            Assert.AreEqual(expected, Formatter.Format(original));
        }

        [TestMethod]
        public void SingleLineTrailingComment()
        {
            var original = @"select * from tbl
-- comment here";
            var expected = @"SELECT *
FROM   tbl;
-- comment here";
            Assert.AreEqual(expected, Formatter.Format(original));
        }

        [TestMethod]
        public void SingleLineInterStatementComment()
        {
            var original = @"select * from tbl
-- comment here
insert into tbl (col) values ('foo')";
            var expected = @"SELECT *
FROM   tbl;
-- comment here
INSERT  INTO tbl (col)
VALUES          ('foo');";
            Assert.AreEqual(expected, Formatter.Format(original));
        }

        [TestMethod]
        public void SingleLineInterTokenComment()
        {
            var original = @"select *
-- comment here
from tbl";
            var expected = @"SELECT *
-- comment here
FROM   tbl;";
            Assert.AreEqual(expected, Formatter.Format(original));
        }

        [TestMethod]
        public void SlashStarInterTokenComment()
        {
            var original = @"select * /* all cols */
from tbl";
            var expected = @"SELECT * /* all cols */
FROM   tbl;";
            Assert.AreEqual(expected, Formatter.Format(original));
        }

        [TestMethod]
        public void Issue408()
        {
            var original = @"SELECT w.*
FROM   workflow AS w CROSS APPLY OPENJSON (JSON_QUERY(w.clientdata, '$.properties.connectionReferences')) AS wfcr
       LEFT OUTER JOIN
       connectionreference AS cr
       ON JSON_VALUE(wfcr.[value], '$.connection.connectionReferenceLogicalName') = cr.connectionreferencelogicalname
WHERE  w.category = 5
       --AND JSON_VALUE(wfcr.[value], '$.connection.connectionReferenceLogicalName') IS NULL;";

            var expected = @"SELECT w.*
FROM   workflow AS w CROSS APPLY OPENJSON (JSON_QUERY(w.clientdata, '$.properties.connectionReferences')) AS wfcr
       LEFT OUTER JOIN
       connectionreference AS cr
       ON JSON_VALUE(wfcr.[value], '$.connection.connectionReferenceLogicalName') = cr.connectionreferencelogicalname
WHERE  w.category = 5;

--AND JSON_VALUE(wfcr.[value], '$.connection.connectionReferenceLogicalName') IS NULL;";

            Assert.AreEqual(expected, Formatter.Format(original));
        }
    }
}
