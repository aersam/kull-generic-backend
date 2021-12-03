using Kull.Data;
using Kull.DatabaseMetadata;
using Kull.GenericBackend.Common;
using Kull.GenericBackend.Utils;
#if NET48
using HttpContext = System.Web.HttpContextBase;
#else
using Microsoft.AspNetCore.Http;
#endif
using Microsoft.OpenApi.Models;
#if NEWTONSOFTJSON
using Newtonsoft.Json;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Kull.GenericBackend.Parameters;

public class DbApiParameter : WebApiParameter
{
    private readonly TableValuedParameter? TableParameter;

    public SqlType DbType { get; }
    public bool IsNullable { get; }

    public DBObjectName? UserDefinedType { get; }

    /// <summary>
    /// Do not add a parameter to db command if not provided by user. use db default
    /// </summary>
    public override bool RequiresUserProvidedValue => true;

    public DbApiParameter(string sqlName, string webApiName,
            SqlType sqlType, bool isNullable,
            DBObjectName? userDefinedType,
            IReadOnlyCollection<SqlFieldDescription>? userDefinedTypeFields,
            SwaggerGeneration.CodeConvention convention,
            NamingMappingHandler namingMappingHandler) : base(sqlName, webApiName)
    {
        this.DbType = sqlType;
        this.IsNullable = isNullable;
        UserDefinedType = userDefinedType;
        if (userDefinedType != null)
        {
            this.TableParameter = new TableValuedParameter(
                convention.GetUserDefinedSqlTypeWebApiName(userDefinedType),
                userDefinedType,
                namingMappingHandler, userDefinedTypeFields ?? throw new ArgumentNullException("userDefinedTypeFields"));
        }
    }





    public override IEnumerable<WebApiParameter> GetRequiredTypes()
    {
        if (TableParameter == null)
        {
            return Array.Empty<WebApiParameter>();
        }
        else
        {
            return new WebApiParameter[] { TableParameter };
        }
    }

    public override OpenApiSchema GetSchema()
    {
        OpenApiSchema property = new OpenApiSchema();
        property.Type = this.DbType.JsType;
        if (this.DbType.JsFormat != null)
        {
            property.Format = this.DbType.JsFormat;
        }
        property.Nullable = this.IsNullable;

        if (this.UserDefinedType != null)
        {
            property.UniqueItems = false;
            property.Items = new OpenApiSchema()
            {
                Reference = new OpenApiReference()
                {
                    Type = ReferenceType.Schema,
                    Id = TableParameter!.WebApiName
                }
            };
        }
        return property;
    }


    private XElement ToXml(IDictionary<string, object> input)
    {
        return new XElement("el",
            input.Keys.Select(k => new XAttribute(k, input[k])));
    }


    private XElement ToXml(IEnumerable<IDictionary<string, object>> input)
    {
        return new XElement("root",
            input.Select(s => ToXml(s)));
    }



    public override object? GetValue(HttpContext? http, object? valueProvided)
    {
        if (valueProvided is IDictionary<string, object> obj)
        {
            if (this.SqlName!.EndsWith("Xml") || this.DbType.DbType == "xml")
            {
                return ToXml(obj)?.ToString();
            }
            else if (this.UserDefinedType != null)
            {
                return this.TableParameter!.GetValue(http, new IDictionary<string, object>[] { obj });
            }
            else
            {
                return JsonHelper.SerializeObject(obj);
            }
        }
        else if (valueProvided is IEnumerable<Dictionary<string, object>> objAr)
        {
            if (this.SqlName!.EndsWith("Xml") || this.DbType.DbType == "xml")
            {
                return ToXml(objAr)?.ToString();
            }
            else if (this.UserDefinedType != null)
            {
                return TableParameter!.GetValue(http, objAr);
            }
            else
            {
                return JsonHelper.SerializeObject(objAr);
            }
        }
#if NEWTONSOFTJSON
        else if (valueProvided is Newtonsoft.Json.Linq.JArray ar)
        {
            if (this.UserDefinedType != null)
            {
                var jobjAr = ar.Cast<Newtonsoft.Json.Linq.JObject>()
                    .Select(oo => oo.Properties()
                        .ToDictionary(p => p.Name, p => p.Value.ToObject<object>())
                        ).ToArray();
                return TableParameter!.GetValue(http, jobjAr);
            }
            else
            {
                return JsonConvert.SerializeObject(valueProvided);
            }
        }
        else if (valueProvided is Newtonsoft.Json.Linq.JObject obj2)
        {
            if (this.UserDefinedType != null)
            {
                var jar_ob = new IDictionary<string, object?>[]
                {
                            obj2.Properties()
                                .ToDictionary(p => p.Name, p => p.Value.ToObject<object?>())

                };
                return TableParameter!.GetValue(http, jar_ob);
            }
            else
            {
                return JsonConvert.SerializeObject(valueProvided);
            }
        }
#endif
        else if (this.DbType.NetType == typeof(System.Byte[]) && valueProvided is string str)
        {
            return Convert.FromBase64String(str);
        }
        else
        {
            return valueProvided;
        }
    }
}
