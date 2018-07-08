﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Converters;
using MercadoPago.DataStructures.Generic;
using System.Linq;

namespace MercadoPago
{
    public static class MPCoreUtils
    {
        /// <summary>
        /// Find specified tokens.
        /// </summary>
        /// <param name="containerToken">Token container.</param>
        /// <param name="name">Name of the token we are searching.</param>
        /// <returns>List of tokens that match with the search parameter.</returns>
        public static List<JToken> FindTokens(this JToken containerToken, string name)
        {
            List<JToken> matches = new List<JToken>();
            FindTokens(containerToken, name, matches);
            return matches;
        } 
        /// <summary>
        /// Core implementation of FindTokens
        /// </summary>
        /// <param name="containerToken">Token container.</param>
        /// <param name="name">Name of the token we are searching.</param>
        /// <param name="matches">List of tokens that match with the search parameter.</param>
        private static void FindTokens(JToken containerToken, string name, List<JToken> matches)
        {
            if (containerToken.Type == JTokenType.Object)
            {
                foreach (JProperty child in containerToken.Children<JProperty>())
                {
                    if (child.Name == name)
                    {
                        matches.Add(child.Value);
                    }
                    FindTokens(child.Value, name, matches);
                }
            }
            else if (containerToken.Type == JTokenType.Array)
            {
                foreach (JToken child in containerToken.Children())
                {
                    FindTokens(child, name, matches);
                }
            }
        }

        /// <summary>
        /// Static method that transforms all attributes members of the instance in a JSON Object.
        /// </summary>
        /// <returns>a JSON Object with the attributes members of the instance</returns>
        public static JObject GetJsonFromResource<T>(T resource) where T : MPBase
        {
            JsonSerializer serializer = new JsonSerializer { 
                NullValueHandling = NullValueHandling.Ignore, 
                ContractResolver = new CustomSerializationContractResolver() 
            };
            serializer.Converters.Add(new IsoDateTimeConverter()
            {
                DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK"
            });
            JObject jobject = JObject.FromObject(resource, serializer); 
            return jobject;
        }

        /// <summary>
        /// Static method that transforms JObject in to a resource.
        /// </summary>
        /// <returns>an object obteined from obj</returns>
        public static MPBase GetResourceFromJson<T>(Type type, JObject jObj) where T : MPBase
        {
            JsonSerializer serializer = new JsonSerializer { 
                NullValueHandling = NullValueHandling.Ignore,  
                ContractResolver = new CustomDeserializationContractResolver()
            };
            serializer.Converters.Add(new IsoDateTimeConverter(){
                DateTimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK"
            });
            T resource = (T)jObj.ToObject<T>(serializer);
            return resource;
        }

        /// <summary>
        /// Static method that transforms JObject in to a resource.
        /// </summary>
        /// <returns>an object obteined from obj</returns>
        public static BadParamsError GetBadParamsError(string raw) 
        {
            JObject jObj = JObject.Parse(raw);

            JsonSerializer serializer = new JsonSerializer
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new CustomDeserializationContractResolver()
            };

            BadParamsError badParams = (BadParamsError)jObj.ToObject<BadParamsError>(serializer);
            return badParams;
        }

        public static JArray GetArrayFromJsonElement(JObject jsonElement)
        {
            JArray jsonArray = null;
            if (jsonElement is JObject)
            {
                jsonArray = JArray.Parse(jsonElement["results"].ToString());
            }
            return jsonArray;
        }

        // TODO: Unneeded type parameter. Remove.
        public static JArray GetArrayFromJsonElement<T>(JObject jsonElement) where T : MPBase =>
            GetArrayFromJsonElement(jsonElement);

        public static JArray GetJArrayFromStringResponse<T>(string stringResponse) where T : MPBase
        {
            JArray jsonArray = null;
            jsonArray = JArray.Parse(stringResponse);
            return jsonArray;
        }

        public static List<T> ToList<T>(this MPAPIResponse response) where T : ResourceBase
        {
            var result = new List<T>();

            var jsonArray =
                response.JsonObjectResponse != null
                    ? GetArrayFromJsonElement(response.JsonObjectResponse)
                    : JArray.Parse(response.StringResponse);

            if (jsonArray != null)
            {
                foreach (var jObject in jsonArray.OfType<JObject>())
                {
                    //TODO: Por que esto deserializa y serializa de nuevo?
                    T resource = jObject.Deserialize<T>();
                    resource.LastKnownJson = resource.Serialize();
                    result.Add(resource);
                }
            }

            return result;
        }
    }
}
