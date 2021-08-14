
#if !NETSTD && !NETFX
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.OpenApi.Models;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.GenericSP;
using Kull.GenericBackend.SwaggerGeneration;
using System.IO;
using System.Data.Common;
using System.Text.Json;
using Kull.GenericBackend.Error;
using System.Data;

namespace Kull.GenericBackend.Serialization
{
    /// <summary>
    /// Helper class for writing the result of a command to the body of the response
    /// </summary>
    public class GenericSPJsonSerializerSTJ : GenericSPJsonSerializerBase, IGenericSPSerializer
    {
        public GenericSPJsonSerializerSTJ(Common.NamingMappingHandler namingMappingHandler, SPMiddlewareOptions options,
                ILogger<GenericSPJsonSerializerBase> logger,
                CodeConvention convention,
                ResponseDescriptor responseDescriptor,
                 Error.JsonErrorHandler jsonErrorHandler) : base(namingMappingHandler, options, logger,
                    convention, responseDescriptor, jsonErrorHandler)
        { }

        protected override async Task WriteCurrentResultSet(Stream outputStream, DbDataReader rdr,
            string[] fieldNamesToUse, bool? firstReadResult, bool objectOfFirstOnly)
        {

            if (options.Encoding.BodyName != "utf-8")
            {
                throw new NotSupportedException("Only utf8 is supported");
            }
            Type[] types = GetTypesFromReader(rdr);
            var jsonWriter = new Utf8JsonWriter(outputStream);

            if (firstReadResult == null)
                firstReadResult = rdr.Read();
            var jsFields = fieldNamesToUse.Select(s => JsonEncodedText.Encode(s)).ToArray();
            if (objectOfFirstOnly)
            {
                if (firstReadResult.Value)
                {
                    WriteSingleRow(rdr, jsFields, types, jsonWriter);
                }
                else
                {
                    jsonWriter.WriteNullValue();
                }
                await jsonWriter.FlushAsync();
                return;
            }

            jsonWriter.WriteStartArray();


            if (firstReadResult == true)
            {
                do
                {
                    WriteSingleRow(rdr, jsFields, types, jsonWriter);
                }
                while (rdr.Read());
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync();
        }

        private static Type[] GetTypesFromReader(System.Data.IDataRecord rdr)
        {
            Type[] types = new Type[rdr.FieldCount];
            for (int i = 0; i < rdr.FieldCount; i++)
            {
                types[i] = rdr.GetFieldType(i);
                var nnType = Nullable.GetUnderlyingType(types[i]);
                if (nnType != null)
                    types[i] = nnType;
            }

            return types;
        }

        static JsonEncodedText charStart = JsonEncodedText.Encode("\"");

        char[] charBuffer;

        private void WriteSingleRow(System.Data.IDataRecord rdr, JsonEncodedText[] fieldNamesToUse, Type[] types, Utf8JsonWriter jsonWriter)
        {
            jsonWriter.WriteStartObject();
            for (int p = 0; p < fieldNamesToUse.Length; p++)
            {
                if (rdr.IsDBNull(p))
                {
                    jsonWriter.WriteNull(fieldNamesToUse[p]);
                }
                else if (types[p] == typeof(string))
                {
#if !NET6_0_OR_GREATER
                    jsonWriter.WriteString(fieldNamesToUse[p], rdr.GetString(p));
#else
                    jsonWriter.WritePropertyName(fieldNamesToUse[p]);
                    jsonWriter.WriteRawValue(charStart.EncodedUtf8Bytes);
                    charBuffer ??= new char[100];
                    long offset = 0;
                    int bytesRead = 0;
                    do
                    {
                        bytesRead = (int)rdr.GetChars(p, offset, charBuffer, 0, charBuffer.Length);
                        if (bytesRead > 0)
                        {
                            ReadOnlySpan<char> rsp = new ReadOnlySpan<char>(charBuffer, 0, bytesRead);
                            jsonWriter.WriteRawValue(rsp);
                        }
                    }
                    while (bytesRead > 0);
                    jsonWriter.WriteRawValue(charStart.EncodedUtf8Bytes);
#endif
                    //rdr.GetChars()
                }
                else if (types[p] == typeof(DateTime))
                {
                    jsonWriter.WriteString(fieldNamesToUse[p], rdr.GetDateTime(p));
                }
                else if (types[p] == typeof(DateTimeOffset))
                {
                    jsonWriter.WriteString(fieldNamesToUse[p], (DateTimeOffset)rdr.GetValue(p));
                }
                else if (types[p] == typeof(bool))
                {
                    jsonWriter.WriteBoolean(fieldNamesToUse[p], rdr.GetBoolean(p));
                }
                else if (types[p] == typeof(Guid))
                {
                    jsonWriter.WriteString(fieldNamesToUse[p], rdr.GetGuid(p));
                }
                else if (types[p] == typeof(short))
                {
                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetInt16(p));
                }
                else if (types[p] == typeof(int))
                {
                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetInt32(p));
                }
                else if (types[p] == typeof(long))
                {
                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetInt64(p));
                }
                else if (types[p] == typeof(float))
                {
                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetFloat(p));
                }
                else if (types[p] == typeof(double))
                {
                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetDouble(p));
                }
                else if (types[p] == typeof(decimal))
                {
                    jsonWriter.WriteNumber(fieldNamesToUse[p], rdr.GetDecimal(p));
                }
                else if (types[p] == typeof(byte[]))
                {
                    jsonWriter.WriteBase64String(fieldNamesToUse[p], (byte[])rdr.GetValue(p));
                }
                else
                {
                    string? vl = rdr.GetValue(p)?.ToString();
                    jsonWriter.WriteString(fieldNamesToUse[p], vl);
                }
            }
            jsonWriter.WriteEndObject();
        }

        protected override async Task WriteObject(Stream outputStream, IDataRecord objectData, string[] fieldNames)
        {

            var jsonWriter = new Utf8JsonWriter(outputStream);
            var types = GetTypesFromReader(objectData);
            var jsFields = fieldNames.Select(s => JsonEncodedText.Encode(s)).ToArray();
            WriteSingleRow(objectData, jsFields, types, jsonWriter);
            await jsonWriter.FlushAsync();
        }
    }
}
#endif
