﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System.Collections.Generic;
using System.IO;
using MarkMpn.Sql4Cds.Export.Contracts;
using Microsoft.SqlTools.ServiceLayer.Utility;

namespace MarkMpn.Sql4Cds.Export.DataStorage
{
    /// <summary>
    /// Factory that creates file reader/writers that process rows in an internal, non-human readable file format
    /// </summary>
    public class ServiceBufferFileStreamFactory : IFileStreamFactory
    {
        /// <summary>
        /// Creates a new <see cref="ServiceBufferFileStreamReader"/> for reading values back from
        /// an SSMS formatted buffer file, file share is ReadWrite to allow concurrent reads/writes to the file.
        /// </summary>
        /// <param name="fileName">The file to read values from</param>
        /// <returns>A <see cref="ServiceBufferFileStreamReader"/></returns>
        public IFileStreamReader GetReader(string fileName)
        {
            return new ServiceBufferFileStreamReader(
                new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            );
        }

        /// <summary>
        /// Creates a new <see cref="ServiceBufferFileStreamWriter"/> for writing values out to an
        /// SSMS formatted buffer file, file share is ReadWrite to allow concurrent reads/writes to the file.
        /// </summary>
        /// <param name="fileName">The file to write values to</param>
        /// <param name="columns">
        /// Ignored in order to fulfil the <see cref="IFileStreamFactory"/> contract.
        /// @TODO: Refactor this out so that save-as writers do not use the same contract as service buffer writers.
        /// </param>
        /// <returns>A <see cref="ServiceBufferFileStreamWriter"/></returns>
        public IFileStreamWriter GetWriter(string fileName, IReadOnlyList<DbColumnWrapper> columns)
        {
            return new ServiceBufferFileStreamWriter(
                new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite)
            );
        }

        /// <summary>
        /// Disposes of a file created via this factory
        /// </summary>
        /// <param name="fileName">The file to dispose of</param>
        public void DisposeFile(string fileName)
        {
            FileUtilities.SafeFileDelete(fileName);
        }
    }
}
