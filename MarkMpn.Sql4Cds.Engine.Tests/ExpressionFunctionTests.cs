﻿using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarkMpn.Sql4Cds.Engine.ExecutionPlan;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkMpn.Sql4Cds.Engine.Tests
{
    [TestClass]
    public class ExpressionFunctionTests
    {
        [TestMethod]
        public void DatePart_Week()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/datepart-transact-sql?view=sql-server-ver16#week-and-weekday-datepart-arguments
            // Assuming default SET DATEFIRST 7 -- ( Sunday )
            var actual = ExpressionFunctions.DatePart("week", (SqlDateTime)new DateTime(2007, 4, 21), DataTypeHelpers.DateTime);
            Assert.AreEqual(16, actual);
        }

        [TestMethod]
        public void DatePart_WeekDay()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/datepart-transact-sql?view=sql-server-ver16#week-and-weekday-datepart-arguments
            // Assuming default SET DATEFIRST 7 -- ( Sunday )
            var actual = ExpressionFunctions.DatePart("weekday", (SqlDateTime)new DateTime(2007, 4, 21), DataTypeHelpers.DateTime);
            Assert.AreEqual(7, actual);
        }

        [TestMethod]
        public void DatePart_TZOffset()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/datepart-transact-sql?view=sql-server-ver16#tzoffset
            var actual = ExpressionFunctions.DatePart("tzoffset", (SqlDateTimeOffset)new DateTimeOffset(2007, 5, 10, 0, 0, 1, TimeSpan.FromMinutes(5 * 60 + 10)), DataTypeHelpers.DateTimeOffset);
            Assert.AreEqual(310, actual);
        }

        [TestMethod]
        public void DatePart_ErrorOnInvalidPartsForTimeValue()
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/datepart-transact-sql?view=sql-server-ver16#default-returned-for-a-datepart-that-isnt-in-a-date-argument
            try
            {
                ExpressionFunctions.DatePart("year", new SqlTime(new TimeSpan(0, 12, 10, 30, 123)), DataTypeHelpers.Time(7));
                Assert.Fail();
            }
            catch (QueryExecutionException ex)
            {
                Assert.AreEqual(9810, ex.Errors.Single().Number);
            }
        }

        [DataTestMethod]
        [DataRow("millisecond", 123)]
        [DataRow("microsecond", 123456)]
        [DataRow("nanosecond", 123456700)]
        public void DatePart_FractionalSeconds(string part, int expected)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/datepart-transact-sql?view=sql-server-ver16#fractional-seconds
            var actual = ExpressionFunctions.DatePart(part, (SqlString)"00:00:01.1234567", DataTypeHelpers.VarChar(100, Collation.USEnglish, CollationLabel.CoercibleDefault));
            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow("20240830")]
        [DataRow("2024-08-31")]
        public void DateAdd_MonthLimitedToDaysInFollowingMonth(string date)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#datepart-argument
            SqlDateParsing.TryParse(date, DateFormat.mdy, out SqlDateTime startDate);
            var actual = ExpressionFunctions.DateAdd("month", 1, startDate, DataTypeHelpers.DateTime);
            Assert.AreEqual(new SqlDateTime(2024, 9, 30), (SqlDateTime)actual);
        }

        [DataTestMethod]
        [DataRow(2147483647)]
        [DataRow(-2147483647)]
        public void DateAdd_ThrowsIfResultIsOutOfRange(int number)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#date-argument
            try
            {
                ExpressionFunctions.DateAdd("year", number, new SqlDateTime(2024, 7, 31), DataTypeHelpers.DateTime);
            }
            catch (QueryExecutionException ex)
            {
                Assert.AreEqual(517, ex.Errors.Single().Number);
            }
        }

        [DataTestMethod]
        [DataRow(-30, 0)]
        [DataRow(29, 0)]
        [DataRow(-31, -1)]
        [DataRow(30, 1)]
        public void DateAdd_SmallDateTimeSeconds(int number, int expectedMinutesDifference)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#return-values-for-a-smalldatetime-date-and-a-second-or-fractional-seconds-datepart
            var startDateTime = new DateTime(2024, 10, 5);
            var actual = ((SqlSmallDateTime)ExpressionFunctions.DateAdd("second", number, new SqlSmallDateTime(startDateTime), DataTypeHelpers.SmallDateTime)).Value;
            var expected = startDateTime.AddMinutes(expectedMinutesDifference);
            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow(-30001, 0)]
        [DataRow(29998, 0)]
        [DataRow(-30002, -1)]
        [DataRow(29999, 1)]
        public void DateAdd_SmallDateTimeMilliSeconds(int number, int expectedMinutesDifference)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#return-values-for-a-smalldatetime-date-and-a-second-or-fractional-seconds-datepart
            var startDateTime = new DateTime(2024, 10, 5);
            var actual = ((SqlSmallDateTime)ExpressionFunctions.DateAdd("millisecond", number, new SqlSmallDateTime(startDateTime), DataTypeHelpers.SmallDateTime)).Value;
            var expected = startDateTime.AddMinutes(expectedMinutesDifference);
            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow("millisecond", 1, 1121111)]
        [DataRow("millisecond", 2, 1131111)]
        [DataRow("microsecond", 1, 1111121)]
        [DataRow("microsecond", 2, 1111131)]
        [DataRow("nanosecond", 49, 1111111)]
        [DataRow("nanosecond", 50, 1111112)]
        [DataRow("nanosecond", 150, 1111113)]
        public void DateAdd_FractionalSeconds(string datepart, int number, int expected)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#fractional-seconds-precision
            var startDateTime = new DateTime(2024, 1, 1, 13, 10, 10).AddTicks(1111111);
            var actual = ExpressionFunctions.DateAdd(datepart, number, new SqlDateTime2(startDateTime), DataTypeHelpers.DateTime2(7)).Value;
            Assert.AreEqual(expected, actual.Ticks % TimeSpan.TicksPerSecond);
        }

        [DataTestMethod]
        [DataRow("year", "2025-01-01 13:10:10.1111111")]
        [DataRow("quarter", "2024-04-01 13:10:10.1111111")]
        [DataRow("month", "2024-02-01 13:10:10.1111111")]
        [DataRow("dayofyear", "2024-01-02 13:10:10.1111111")]
        [DataRow("day", "2024-01-02 13:10:10.1111111")]
        [DataRow("week", "2024-01-08 13:10:10.1111111")]
        [DataRow("weekday", "2024-01-02 13:10:10.1111111")]
        [DataRow("hour", "2024-01-01 14:10:10.1111111")]
        [DataRow("minute", "2024-01-01 13:11:10.1111111")]
        [DataRow("second", "2024-01-01 13:10:11.1111111")]
        [DataRow("millisecond", "2024-01-01 13:10:10.1121111")]
        [DataRow("microsecond", "2024-01-01 13:10:10.1111121")]
        [DataRow("nanosecond", "2024-01-01 13:10:10.1111111")]
        public void DateAdd_DateParts(string datepart, string expected)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#a-increment-datepart-by-an-interval-of-1
            var startDateTime = new DateTime(2024, 1, 1, 13, 10, 10).AddTicks(1111111);
            var actual = ExpressionFunctions.DateAdd(datepart, 1, new SqlDateTime2(startDateTime), DataTypeHelpers.DateTime2(7)).Value;
            Assert.AreEqual(expected, actual.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
        }

        [DataTestMethod]
        [DataRow("quarter", 4, "2025-01-01 01:01:01.1111111")]
        [DataRow("month", 13, "2025-02-01 01:01:01.1111111")]
        [DataRow("dayofyear", 366, "2025-01-01 01:01:01.1111111")] // NOTE: Docs used 365, but 2024 is a leap year
        [DataRow("day", 366, "2025-01-01 01:01:01.1111111")] // NOTE: Docs used 365, but 2024 is a leap year
        [DataRow("week", 5, "2024-02-05 01:01:01.1111111")]
        [DataRow("weekday", 31, "2024-02-01 01:01:01.1111111")]
        [DataRow("hour", 23, "2024-01-02 00:01:01.1111111")]
        [DataRow("minute", 59, "2024-01-01 02:00:01.1111111")]
        [DataRow("second", 59, "2024-01-01 01:02:00.1111111")]
        [DataRow("millisecond", 1, "2024-01-01 01:01:01.1121111")]
        public void DateAdd_Carry(string datepart, int number, string expected)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#b-increment-more-than-one-level-of-datepart-in-one-statement
            var startDateTime = new DateTime(2024, 1, 1, 1, 1, 1).AddTicks(1111111);
            var actual = ExpressionFunctions.DateAdd(datepart, number, new SqlDateTime2(startDateTime), DataTypeHelpers.DateTime2(7)).Value;
            Assert.AreEqual(expected, actual.ToString("yyyy-MM-dd HH:mm:ss.fffffff"));
        }

        [DataTestMethod]
        [DataRow("microsecond")]
        [DataRow("nanosecond")]
        public void DateAdd_MicroSecondAndNanoSecondNotSupportedForSmallDateTime(string datepart)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#fractional-seconds-precision
            try
            {
                ExpressionFunctions.DateAdd(datepart, 1, new SqlSmallDateTime(new DateTime(2024, 1, 1)), DataTypeHelpers.SmallDateTime);
                Assert.Fail();
            }
            catch (QueryExecutionException ex)
            {
                Assert.AreEqual(9810, ex.Errors.Single().Number);
            }
        }

        [DataTestMethod]
        [DataRow("microsecond")]
        [DataRow("nanosecond")]
        public void DateAdd_MicroSecondAndNanoSecondNotSupportedForDate(string datepart)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#fractional-seconds-precision
            try
            {
                ExpressionFunctions.DateAdd(datepart, 1, new SqlDate(new DateTime(2024, 1, 1)), DataTypeHelpers.Date);
                Assert.Fail();
            }
            catch (QueryExecutionException ex)
            {
                Assert.AreEqual(9810, ex.Errors.Single().Number);
            }
        }

        [DataTestMethod]
        [DataRow("microsecond")]
        [DataRow("nanosecond")]
        public void DateAdd_MicroSecondAndNanoSecondNotSupportedForDateTime(string datepart)
        {
            // https://learn.microsoft.com/en-us/sql/t-sql/functions/dateadd-transact-sql?view=sql-server-ver16#fractional-seconds-precision
            try
            {
                ExpressionFunctions.DateAdd(datepart, 1, new SqlDateTime(new DateTime(2024, 1, 1)), DataTypeHelpers.DateTime);
                Assert.Fail();
            }
            catch (QueryExecutionException ex)
            {
                Assert.AreEqual(9810, ex.Errors.Single().Number);
            }
        }
    }
}
