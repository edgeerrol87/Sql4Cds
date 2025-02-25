﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Linq;
using MarkMpn.Sql4Cds.Engine.ExecutionPlan;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace MarkMpn.Sql4Cds.Engine
{
    class Sql4CdsDataReader : DbDataReader
    {
        private readonly Sql4CdsConnection _connection;
        private readonly Sql4CdsCommand _command;
        private readonly IQueryExecutionOptions _options;
        private readonly CommandBehavior _behavior;
        private readonly LayeredDictionary<string, DataTypeReference> _parameterTypes;
        private readonly LayeredDictionary<string, INullable> _parameterValues;
        private readonly Stack<Sql4CdsError> _errorDetails;
        private readonly NodeExecutionContext _context;
        private Dictionary<string, int> _labelIndexes;
        private int _recordsAffected;
        private int _instructionPointer;
        private IDataReaderExecutionPlanNode _readerQuery;
        private DbDataReader _reader;
        private bool _error;
        private IRootExecutionPlanNode _logNode;
        private int _rows;
        private int _resultSetsReturned;
        private bool _closed;
        
        public Sql4CdsDataReader(Sql4CdsCommand command, IQueryExecutionOptions options, CommandBehavior behavior)
        {
            _connection = (Sql4CdsConnection)command.Connection;
            _command = command;
            _options = options;
            _behavior = behavior;
            _recordsAffected = -1;

            _parameterTypes = new LayeredDictionary<string, DataTypeReference>(
                ((Sql4CdsParameterCollection)command.Parameters).GetParameterTypes(),
                new Dictionary<string, DataTypeReference>(StringComparer.OrdinalIgnoreCase));

            _parameterValues = new LayeredDictionary<string, INullable>(
                ((Sql4CdsParameterCollection)command.Parameters).GetParameterValues(),
                new Dictionary<string, INullable>(StringComparer.OrdinalIgnoreCase));

            _errorDetails = new Stack<Sql4CdsError>();

            _context = new NodeExecutionContext(_connection.Session, _options, _parameterTypes, _parameterValues, OnInfoMessage);

            if (!NextResult())
                Close();
        }

        private void OnInfoMessage(Sql4CdsError error)
        {
            _connection.OnInfoMessage(_logNode, error);
        }

        internal IDictionary<string, INullable> ParameterValues => _parameterValues;

        private Dictionary<string, int> LabelIndexes
        {
            get
            {
                if (_labelIndexes != null)
                    return _labelIndexes;

                _labelIndexes = _command.Plan
                    .Select((node, index) => new { node, index })
                    .Where(n => n.node is GotoLabelNode)
                    .ToDictionary(n => ((GotoLabelNode)n.node).Label, n => n.index);

                return _labelIndexes;
            }
        }

        private bool ExecuteWithExceptionHandling()
        {
            while (true)
            {
                try
                {
                    return Execute();
                }
                catch (Sql4CdsException ex)
                {
                    _connection.Session.GlobalVariableValues["@@ERROR"] = (SqlInt32)ex.Number;

                    // If we are in a TRY block, store the exception information in the context and move to the CATCH block
                    var caught = false;

                    while (_instructionPointer < _command.Plan.Length && !_options.CancellationToken.IsCancellationRequested)
                    {
                        if (_command.Plan[_instructionPointer] is BeginCatchNode)
                        {
                            caught = true;
                            break;
                        }

                        _instructionPointer++;
                    }

                    if (!caught)
                        throw;

                    _errorDetails.Push(ex.Errors[0]);
                }
            }
        }

        private bool Execute()
        {
            _context.Error = _errorDetails.FirstOrDefault();

            try
            {
                while (_instructionPointer < _command.Plan.Length && !_options.CancellationToken.IsCancellationRequested)
                {
                    var node = _command.Plan[_instructionPointer];

                    if (node is IJitStatement unparsed)
                    {
                        var converted = unparsed.Compile();
                        var newPlan = new IRootExecutionPlanNodeInternal[_command.Plan.Length - 1 + converted.Length];
                        Array.Copy(_command.Plan, 0, newPlan, 0, _instructionPointer);
                        Array.Copy(converted, 0, newPlan, _instructionPointer, converted.Length);
                        Array.Copy(_command.Plan, _instructionPointer + 1, newPlan, _instructionPointer + converted.Length, _command.Plan.Length - _instructionPointer - 1);
                        _command.Plan = newPlan;
                        node = _command.Plan[_instructionPointer];
                        _labelIndexes = null;
                    }

                    _logNode = node;

                    if (node is IDataReaderExecutionPlanNode dataSetNode)
                    {
                        if (_resultSetsReturned == 0 || (!_behavior.HasFlag(CommandBehavior.SingleResult) && !_behavior.HasFlag(CommandBehavior.SingleRow)))
                        {
                            _readerQuery = (IDataReaderExecutionPlanNode)dataSetNode.Clone();
                            _reader = _readerQuery.Execute(_context, _behavior);
                            _resultSetsReturned++;
                            _rows = 0;
                            _instructionPointer++;
                            return true;
                        }
                        else
                        {
                            _rows = 0;
                            _instructionPointer++;
                        }
                    }
                    else if (node is IDmlQueryExecutionPlanNode dmlNode)
                    {
                        dmlNode = (IDmlQueryExecutionPlanNode)dmlNode.Clone();
                        dmlNode.Execute(_context, out var recordsAffected, out var message);

                        _command.OnStatementCompleted(dmlNode, recordsAffected, message);
                        _connection.Session.GlobalVariableValues["@@ERROR"] = (SqlInt32)0;

                        if (recordsAffected != -1)
                        {
                            if (_recordsAffected == -1)
                                _recordsAffected = 0;

                            _recordsAffected += recordsAffected;
                        }
                    }
                    else if (node is IGoToNode cond)
                    {
                        cond = (IGoToNode)cond.Clone();
                        var label = cond.Execute(_context);

                        if (cond.GetSources().Any())
                            _command.OnStatementCompleted(cond, -1, null);

                        if (label != null)
                        {
                            // Move to the label. If we leave a CATCH block, remove the error
                            var catchDepth = 0;

                            var targetIndex = LabelIndexes[label];

                            while (targetIndex < _instructionPointer)
                            {
                                _instructionPointer--;

                                if (_command.Plan[_instructionPointer] is BeginCatchNode)
                                    catchDepth--;
                                else if (_command.Plan[_instructionPointer] is EndCatchNode)
                                    catchDepth++;
                            }

                            while (targetIndex > _instructionPointer)
                            {
                                _instructionPointer++;

                                if (_command.Plan[_instructionPointer] is EndCatchNode)
                                    catchDepth--;
                                else if (_command.Plan[_instructionPointer] is BeginCatchNode)
                                    catchDepth++;
                            }

                            while (catchDepth < 0)
                            {
                                _errorDetails.Pop();
                                catchDepth++;
                            }

                            _context.Error = _errorDetails.FirstOrDefault();
                        }
                    }
                    else if (node is GotoLabelNode)
                    {
                        // NOOP
                    }
                    else if (node is BeginTryNode)
                    {
                        // TODO
                    }
                    else if (node is EndTryNode)
                    {
                        // Got to the end of the TRY block without error - skip the CATCH block
                        var catchDepth = 0;
                        _instructionPointer++;

                        while (_instructionPointer < _command.Plan.Length)
                        {
                            if (_command.Plan[_instructionPointer] is BeginCatchNode)
                                catchDepth++;
                            else if (_command.Plan[_instructionPointer] is EndCatchNode)
                                catchDepth--;

                            if (catchDepth == 0)
                                break;

                            _instructionPointer++;
                        }
                    }
                    else if (node is BeginCatchNode)
                    {
                        // NOOP
                    }
                    else if (node is EndCatchNode)
                    {
                        // Remove error information from context
                        _errorDetails.Pop();
                        _context.Error = _errorDetails.FirstOrDefault();
                    }
                    else
                    {
                        throw new NotImplementedException("Unexpected node type " + node.GetType().Name);
                    }

                    if (node is IImpersonateRevertExecutionPlanNode)
                    {
                        // TODO: Update options.UserId
                    }

                    _instructionPointer++;
                }
            }
            catch (OperationCanceledException)
            {
                throw new Sql4CdsException(new Sql4CdsError(11, 0, 0, null, null, 0, _command.CancelledManually ? "Query was cancelled by user" : "Query timed out", null));
            }
            catch (Sql4CdsException ex)
            {
                SetErrorLineNumbers(ex, _command.Plan[_instructionPointer]);
                _error = true;
                throw;
            }
            catch (Exception ex)
            {
                _error = true;

                if (ex is ISql4CdsErrorException sqlEx)
                {
                    foreach (var err in sqlEx.Errors)
                    {
                        if (err.LineNumber == -1)
                            err.LineNumber = _command.Plan[_instructionPointer].LineNumber;
                    }

                    if (sqlEx.Errors.Any(err => err.Class > 10))
                        throw new Sql4CdsException(ex.Message, ex);

                    foreach (var err in sqlEx.Errors)
                        _context.Log(err);
                }
                else
                {
                    var sqlErr = new Sql4CdsException(ex.Message, ex);
                    SetErrorLineNumbers(sqlErr, _command.Plan[_instructionPointer]);
                    throw sqlErr;
                }
            }

            if (_options.CancellationToken.IsCancellationRequested)
                throw new Sql4CdsException(new Sql4CdsError(11, 0, 0, null, null, 0, _command.CancelledManually ? "Query was cancelled by user" : "Query timed out", null));

            return false;
        }

        private void SetErrorLineNumbers(Sql4CdsException ex, IRootExecutionPlanNode node)
        {
            if (ex.Errors != null)
            {
                foreach (var err in ex.Errors)
                {
                    if (err.LineNumber == -1)
                        err.LineNumber = node.LineNumber;
                }
            }
        }

        public override object this[int ordinal]
        {
            get
            {
                if (_reader == null)
                    throw new InvalidOperationException();

                var value = _reader[ordinal];

                if (_connection.ReturnEntityReferenceAsGuid && value is SqlEntityReference er)
                    value = er.Id;

                return value;
            }
        }

        public override object this[string name]
        {
            get
            {
                if (_reader == null)
                    throw new InvalidOperationException();

                var value = _reader[name];

                if (_connection.ReturnEntityReferenceAsGuid && value is SqlEntityReference er)
                    value = er.Id;

                return value;
            }
        }

        public override int Depth => 0;

        public override int FieldCount
        {
            get
            {
                if (_reader == null)
                    throw new InvalidOperationException();

                return _reader.FieldCount;
            }
        }

        public override bool HasRows => _reader != null;

        public override bool IsClosed => _closed;

        public override int RecordsAffected => _recordsAffected;

        public override bool GetBoolean(int ordinal)
        {
            return (bool)this[ordinal];
        }

        public override byte GetByte(int ordinal)
        {
            return (byte)this[ordinal];
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            var bytes = (byte[])this[ordinal];

            if (buffer == null)
                return bytes.Length;

            length = (int) Math.Min(length, bytes.Length - dataOffset);
            Array.Copy(bytes, dataOffset, buffer, bufferOffset, length);
            return length;
        }

        public override char GetChar(int ordinal)
        {
            return (char)this[ordinal];
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            var chars = ((string)this[ordinal]).ToCharArray();

            if (buffer == null)
                return chars.Length;

            length = (int)Math.Min(length, chars.Length - dataOffset);
            Array.Copy(chars, dataOffset, buffer, bufferOffset, length);
            return length;
        }

        public override string GetDataTypeName(int ordinal)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            var type = _reader.GetDataTypeName(ordinal);

            if (_connection.ReturnEntityReferenceAsGuid && type == typeof(SqlEntityReference).FullName)
                type = "uniqueidentifier";

            return type;
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return (DateTime)this[ordinal];
        }

        public override decimal GetDecimal(int ordinal)
        {
            return (decimal)this[ordinal];
        }

        public override double GetDouble(int ordinal)
        {
            return (double)this[ordinal];
        }

        public override IEnumerator GetEnumerator()
        {
            while (Read())
                yield return this;
        }

        public override Type GetFieldType(int ordinal)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            var type = _reader.GetFieldType(ordinal);

            if (_connection.ReturnEntityReferenceAsGuid && type == typeof(SqlEntityReference))
                type = typeof(Guid);

            return type;
        }

        public override float GetFloat(int ordinal)
        {
            return (float)this[ordinal];
        }

        public override Guid GetGuid(int ordinal)
        {
            var value = this[ordinal];
            if (value is SqlEntityReference er)
                return (Guid)er;
            else
                return (Guid)value;
        }

        public override short GetInt16(int ordinal)
        {
            return (short)this[ordinal];
        }

        public override int GetInt32(int ordinal)
        {
            return (int)this[ordinal];
        }

        public override long GetInt64(int ordinal)
        {
            return (long)this[ordinal];
        }

        public override string GetName(int ordinal)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            return _reader.GetName(ordinal);
        }

        public override int GetOrdinal(string name)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            return _reader.GetOrdinal(name);
        }

        public override Type GetProviderSpecificFieldType(int ordinal)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            return _reader.GetProviderSpecificFieldType(ordinal);
        }

        public override object GetProviderSpecificValue(int ordinal)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            return _reader.GetProviderSpecificValue(ordinal);
        }

        public override int GetProviderSpecificValues(object[] values)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            return _reader.GetProviderSpecificValues(values);
        }

        public override string GetString(int ordinal)
        {
            return (string)this[ordinal];
        }

        public override object GetValue(int ordinal)
        {
            return this[ordinal];
        }

        public override int GetValues(object[] values)
        {
            var length = Math.Min(values.Length, FieldCount);

            for (var i = 0; i < length; i++)
                values[i] = this[i];

            return length;
        }

        public override bool IsDBNull(int ordinal)
        {
            if (_reader == null)
                throw new InvalidOperationException();

            return _reader.IsDBNull(ordinal);
        }

        public override bool NextResult()
        {
            return ExecuteWithExceptionHandling();
        }

        public override bool Read()
        {
            if (_reader == null)
                throw new InvalidOperationException();

            bool read;

            try
            {
                read = _reader.Read();
            }
            catch (Exception ex)
            {
                _error = true;
                _reader.Close();
                _reader = null;

                var sqlErr = ex as Sql4CdsException;
                var rethrow = true;

                if (sqlErr == null)
                {
                    if (ex is OperationCanceledException)
                        sqlErr = new Sql4CdsException(new Sql4CdsError(11, 0, 0, null, null, 0, _command.CancelledManually ? "Query was cancelled by user" : "Query timed out", null));
                    else
                        sqlErr = new Sql4CdsException(ex.Message, ex);

                    rethrow = false;
                }

                _connection.Session.GlobalVariableValues["@@ERROR"] = (SqlInt32)sqlErr.Number;
                SetErrorLineNumbers(sqlErr, _readerQuery);
                _readerQuery = null;

                if (rethrow)
                    throw;
                else
                    throw sqlErr;
            }

            if (read)
            {
                _rows++;
            }
            else
            {
                _command.OnStatementCompleted(_readerQuery, _rows, $"({_rows} {(_rows == 1 ? "row" : "rows")} affected)");
                _connection.Session.GlobalVariableValues["@@ERROR"] = (SqlInt32)0;

                _reader.Close();
                _reader = null;
                _readerQuery = null;
            }

            return read;
        }

        public override DataTable GetSchemaTable()
        {
            if (_reader == null)
                throw new InvalidOperationException();

            var schemaTable = _reader.GetSchemaTable();

            var clone = schemaTable.Clone();

            foreach (DataRow row in schemaTable.Rows)
                clone.ImportRow(row);

            var dataTypeCol = clone.Columns.IndexOf("DataType");
            var dataTypeNameCol = clone.Columns.IndexOf("DataTypeName");
            var providerSpecificDataTypeCol = clone.Columns.IndexOf("ProviderSpecificDataType");

            foreach (DataRow cloneRow in clone.Rows)
            {
                if (dataTypeCol != -1 && cloneRow[dataTypeCol] is Type t)
                {
                    if (t == typeof(SqlEntityReference) && _connection.ReturnEntityReferenceAsGuid)
                    {
                        cloneRow[dataTypeCol] = typeof(Guid);
                    }
                    else if (t == typeof(SqlDateTime2) || t == typeof(SqlDate))
                    {
                        cloneRow[dataTypeCol] = typeof(DateTime);
                        cloneRow[providerSpecificDataTypeCol] = typeof(DateTime);
                    }
                    else if (t == typeof(SqlDateTimeOffset))
                    {
                        cloneRow[dataTypeCol] = typeof(DateTimeOffset);
                        cloneRow[providerSpecificDataTypeCol] = typeof(DateTimeOffset);
                    }
                    else if (t == typeof(SqlTime))
                    {
                        cloneRow[dataTypeCol] = typeof(TimeSpan);
                        cloneRow[providerSpecificDataTypeCol] = typeof(TimeSpan);
                    }
                }

                if (dataTypeNameCol != -1 && cloneRow[dataTypeNameCol] is string s && s == nameof(DataTypeHelpers.EntityReference))
                    cloneRow[dataTypeNameCol] = "uniqueidentifier";
            }

            return clone;
        }

        public override void Close()
        {
            if (_closed)
                return;

            if (!_error)
            {
                while (_reader != null && Read())
                    ;

                while (NextResult())
                {
                    while (Read())
                        ;
                }
            }

            if (_options is IDisposable disposableOptions)
                disposableOptions.Dispose();

            _closed = true;
            base.Close();
        }
    }
}
