// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;

#nullable enable
namespace Microsoft.Build.Tasks.ResolveAssemblyReferences
{
    internal static class RpcUtils
    {
        internal static void GetRarMessageHandler(Stream stream)
        //internal static IJsonRpcMessageHandler GetRarMessageHandler(Stream stream)
        {
            throw new NotImplementedException();
            //TODO: remove the func
            //return new LengthHeaderMessageHandler(stream.UsePipe(), new MessagePackFormatter());
        }
    }
}
