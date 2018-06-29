using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using MercadoPago.DataStructures.Generic;

namespace MercadoPago
{
    public abstract class MPBase
    {
        public static bool WITHOUT_CACHE = false;
        public static bool WITH_CACHE = true;
        public static List<string> ALLOWED_BULK_METHODS = new List<string>() { "All", "Search" };

        public static string IdempotencyKey { get; set; }

        protected MPAPIResponse _lastApiResponse;
        protected JObject _lastKnownJson;


        protected RecuperableError _errors;
        public RecuperableError Errors
        {
            get { return  _errors; }
            private set { _errors = value; }
        }

        #region Errors Definitions
        public static string DataTypeError  = "Error on property #PROPERTY. The value you are trying to assign has not the correct type. ";
        public static string RangeError     = "Error on property #PROPERTY. The value you are trying to assign is not in the specified range. ";
        public static string RequiredError  = "Error on property #PROPERTY. There is no value for this required property. ";
        public static string RegularExpressionError = "Error on property #PROPERTY. The specified value is not valid. RegExp: #REGEXPR . ";
        #endregion

        #region Core Methods

        /// <summary>
        /// Gets the last api response for the resource.
        /// </summary>
        /// <returns>Last Api response.</returns>
        public MPAPIResponse GetLastApiResponse()
        {
            return this._lastApiResponse;
        }

        /// <summary>
        /// Gets the last api response for the resource.
        /// </summary>
        /// <returns>Last Api response.</returns>
        public JObject GetLastKnownJson()
        {
            return this._lastKnownJson;
        } 
        /// <summary>
        /// Checks if current resource needs idempotency key and set IdempotencyKey if positive.
        /// </summary>
        /// <param name="classType">ClassType.</param>        
        public static void AdmitIdempotencyKey(Type classType)
        {
            var attribute = classType.GetCustomAttributes(true);

            foreach (Attribute attr in attribute)
            {
                if (attr.GetType() == typeof(Idempotent))
                {
                    IdempotencyKey = attr.GetType().GUID.ToString();
                }
            }
        }

        public static List<T> ProcessMethodBulk<T>(Type clazz, string methodName, bool useCache) where T : MPBase
        {
            Dictionary<string, string> mapParams = null;
            return ProcessMethodBulk<T>(clazz, methodName, mapParams, useCache);
        }
    
        public static List<T> ProcessMethodBulk<T>(Type clazz, string methodName, string param1, bool useCache) where T : MPBase
        {
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            mapParams.Add("param1", param1);
            return ProcessMethodBulk<T>(clazz, methodName, mapParams, useCache);
        }

        /// <summary>
        /// Retrieve a MPBase resource based on a specfic method and configuration.
        /// </summary>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>MPBase resource.</returns>
        public static MPBase ProcessMethod(string methodName, bool useCache)
        {
            Type classType = GetTypeFromStack();
            AdmitIdempotencyKey(classType);
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            return ProcessMethod<MPBase>(classType, null, methodName, mapParams, useCache);
        }


        /// <summary>
        /// Retrieve a MPBase resource based on a specfic method, parameters and configuration.
        /// </summary>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="param">Parameters to use in the retrieve process.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>MPBase resource.</returns>
        public static MPBase ProcessMethod<T>(Type type, string methodName, string param, bool useCache) where T : MPBase
        {
            Type classType = GetTypeFromStack();
            AdmitIdempotencyKey(classType);
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            mapParams.Add("param0", param);

            return ProcessMethod<T>(classType, null, methodName, mapParams, useCache);
        }

        public static MPBase ProcessMethod<T>(Type clazz, string methodName, string param1, string param2, bool useCache) where T : MPBase
        {
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            mapParams.Add("param0", param1);
            mapParams.Add("param1", param2);

            return ProcessMethod<T>(clazz, null, methodName, mapParams, useCache);
        }

        /// <summary>
        /// Retrieve a MPBase resource based on a specfic method, parameters and configuration.
        /// </summary>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="param">Parameters to use in the retrieve process.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>MPBase resource.</returns>
        public static MPBase ProcessMethod<T>(string methodName, string param, bool useCache) where T : MPBase
        {
            Type classType = GetTypeFromStack();
            AdmitIdempotencyKey(classType);
            Dictionary<string, string> mapParams = new Dictionary<string, string>();
            mapParams.Add("param0", param);
            return ProcessMethod<T>(classType, null, methodName, mapParams, useCache);
        }

        /// <summary>
        /// Retrieve a MPBase resource based on a specific method and configuration.       
        /// </summary>
        /// <typeparam name="T">Object derived from MPBase abstract class.</typeparam>
        /// <param name="methodName">Name of the method we are trying to call.</param>
        /// <param name="useCache">Cache configuration</param>
        /// <returns>MPBase resource.</returns>
        public MPBase ProcessMethod<T>(string methodName, bool useCache) where T : MPBase
        {
            Dictionary<string, string> mapParams = null;
            T resource = ProcessMethod<T>(this.GetType(), (T)this, methodName, mapParams, useCache);
            return (T)this;
        }

        protected static List<T> ProcessMethodBulk<T>(Type clazz, string methodName, Dictionary<string, string> mapParams, bool useCache) where T : MPBase
        {
 

            //Validates the method executed
            if (!ALLOWED_BULK_METHODS.Contains(methodName))
            {
                throw new MPException("Method \"" + methodName + "\" not allowed");
            }

            List<T> resourcesList = new List<T>();

            var annotatedMethod = GetAnnotatedMethod(clazz, methodName);
            var hashAnnotation = GetRestInformation(annotatedMethod);
            HttpMethod httpMethod = (HttpMethod)hashAnnotation["method"];
            T resource = null;
            string path = ParsePath(hashAnnotation["path"].ToString(), mapParams, resource);
            int retries = (int)hashAnnotation["retries"];
            int connectionTimeout = (int)hashAnnotation["requestTimeout"];
            Console.WriteLine("Path: {0}", path); 
            PayloadType payloadType = (PayloadType)hashAnnotation["payloadType"];
            WebHeaderCollection colHeaders = GetStandardHeaders();

            MPAPIResponse response = CallAPI(httpMethod, path, payloadType, null, colHeaders, useCache, connectionTimeout, retries);
            
            List<T> resourceArray = new List<T>();

            if (response.StatusCode >= 200 &&
                    response.StatusCode < 300)
            { 
                resourceArray = FillArrayWithResponseData<T>(clazz, response); 
            }

            return resourceArray;
        }


        /// <summary>
        /// Core implementation of processMethod. Retrieves a generic type. 
        /// </summary>
        /// <typeparam name="T">Generic type that will return.</typeparam>
        /// <param name="clazz">Type of Class we are using.</param>
        /// <param name="resource">Resource we will use and return in the implementation.</param>
        /// <param name="methodName">The name of the method  we are trying to call.</param>
        /// <param name="parameters">Parameters to use in the process.</param>
        /// <param name="useCache">Cache configuration.</param>
        /// <returns>Generic type object, containing information about retrieval process.</returns>
        protected static T ProcessMethod<T>(Type clazz, T resource, string methodName, Dictionary<string, string> parameters, bool useCache) where T : MPBase
        {
            if (resource == null)
            {
                try
                {
                    resource = (T)Activator.CreateInstance(clazz);
                }
                catch (Exception ex)
                {
                    throw new MPException(ex.Message);
                }
            }

            var clazzMethod = GetAnnotatedMethod(clazz, methodName);
            var restData = GetRestInformation(clazzMethod);

            HttpMethod httpMethod = (HttpMethod)restData["method"]; 
            string path = ParsePath(restData["path"].ToString(), parameters, resource); 
            PayloadType payloadType = (PayloadType)restData["payloadType"];
            JObject payload = GeneratePayload(httpMethod, resource);  

            int requestTimeout = (int)restData["requestTimeout"];
            int retries = (int)restData["retries"]; 
            WebHeaderCollection colHeaders = new WebHeaderCollection(); 
            MPAPIResponse response = CallAPI(httpMethod, path, payloadType, payload, colHeaders, useCache, requestTimeout, retries);

            if (response.StatusCode >= 200 && response.StatusCode < 300)
            {
                if (httpMethod != HttpMethod.DELETE)
                {
                    resource = (T)FillResourceWithResponseData(resource, response);
                    resource._lastApiResponse = response;
                }
                else
                {
                    resource = null;
                }
            } else if (response.StatusCode >= 400 && response.StatusCode < 500) { 
                BadParamsError badParamsError = MPCoreUtils.GetBadParamsError(response.StringResponse); 
                resource.Errors = badParamsError;
            } else {
                
                MPException webserverError = new MPException()
                {
                    StatusCode = response.StatusCode,
                    ErrorMessage = response.StringResponse
                };

                webserverError.Cause.Add(response.JsonObjectResponse.ToString()); 

            }


            return resource;
        }  

        /// <summary>
        /// Transforms all attributes members of the instance in a JSON String. Only for POST and PUT methods.
        /// POST gets the full object in a JSON object.
        /// PUT gets only the differences with the last known state of the object.
        /// </summary>
        /// <returns>a JSON Object with the attributes members of the instance. Null for GET and DELETE methods</returns>
        public static JObject GeneratePayload<T>(HttpMethod httpMethod, T resource) where T : MPBase
        {
            if (httpMethod.ToString() == "POST" || httpMethod.ToString() == "PUT")
            {  
                JObject actualJSON = MPCoreUtils.GetJsonFromResource(resource);
                JObject oldJSON = resource.GetLastKnownJson();
                return getDiffFromLastChange(actualJSON, oldJSON);
            }
            else
            {
                return null;
            }
        }

        public static JObject getDiffFromLastChange(JToken jactual, JToken jold)
        {
            JObject new_jobject = new JObject();
 
            if (((JObject)jactual).Properties().Count() > 0)
            {
                foreach (JProperty x in ((JObject)jactual).Properties())
                { 
                    string key = ToSnakeCase(x.Name); 

                    if (x.Value.GetType() == typeof(JObject))
                    {
                        if (jold != null)
                        {
                            var new_value = getDiffFromLastChange(x.Value, ((JObject)jold[x.Name]));
                            if (new_value != null)
                            {
                                if (new_value.Properties().Count() > 0)
                                {
                                    new_jobject.Add(key, new_value);
                                }
                            }
                        }
                        else
                        {
                            new_jobject.Add(key, x.Value);
                        }
                    }
                    else if (x.Value.GetType() == typeof(JArray))
                    {
                        new_jobject.Add(key, x.Value);
                    }
                    else if (x.Value.GetType() == typeof(JValue))
                    {
                        if (jold != null)
                        {
                            if (jold[x.Name] != null)
                            {
                                if ((string)x.Value != (string)jold[x.Name])
                                {
                                    new_jobject.Add(key, x.Value);
                                }
                            }
                            else
                            {
                                new_jobject.Add(key, x.Value);
                            }
                        }
                        else
                        {
                            new_jobject.Add(key, x.Value);
                        }
                    }
                }
                return new_jobject;
            }
            else
            {
                return null;
            }
        }
 
        /// <summary>
        /// Fills all the attributes members of the Resource obj.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="resource"></param>
        /// <param name="response"></param>
        /// <returns></returns>
        protected static MPBase FillResourceWithResponseData<T>(T resource, MPAPIResponse response) where T : MPBase
        {
            if (response.JsonObjectResponse != null &&
                    response.JsonObjectResponse is JObject)
            {
                JObject jsonObject = null;

                jsonObject = (JObject)response.JsonObjectResponse;
                T resourceObject = (T)MPCoreUtils.GetResourceFromJson<T>(resource.GetType(), jsonObject);
                resource = (T)FillResource(resourceObject, resource);
                resource._lastKnownJson = MPCoreUtils.GetJsonFromResource(resource);
            }

            return resource;
        }

        protected static List<T> FillArrayWithResponseData<T>(Type clazz, MPAPIResponse response) where T : MPBase
        {
            List<T> resourceArray = new List<T>();
            if (response.JsonObjectResponse != null)
            {
                JArray jsonArray = MPCoreUtils.GetArrayFromJsonElement<T>(response.JsonObjectResponse); 

                if (jsonArray != null)
                {
                    for (int i = 0; i < jsonArray.Count(); i++)
                    {
                        T resource = (T)MPCoreUtils.GetResourceFromJson<T>(clazz, (JObject)jsonArray[i]);
                        resource._lastKnownJson = MPCoreUtils.GetJsonFromResource(resource);
                        resourceArray.Add(resource);
                    }
                }
            } else {
                JArray jsonArray = MPCoreUtils.GetJArrayFromStringResponse<T>(response.StringResponse);
                if (jsonArray != null)
                {
                    for (int i = 0; i < jsonArray.Count(); i++)
                    {
                        T resource = (T)MPCoreUtils.GetResourceFromJson<T>(clazz, (JObject)jsonArray[i]);
                        resource._lastKnownJson = MPCoreUtils.GetJsonFromResource(resource);
                        resourceArray.Add(resource);
                    }
                }
            }
            return resourceArray;
        }

        /// <summary>
        /// Fills all the attributes members of the Resource obj.
        /// </summary>
        /// <returns>MPBase object with response attributes.</returns>
        private static MPBase FillResource<T>(T sourceResource, T destinationResource) where T : MPBase
        {
            FieldInfo[] declaredFields = destinationResource.GetType().GetFields(BindingFlags.Instance |
                                                                                   BindingFlags.Static |
                                                                                   BindingFlags.NonPublic |
                                                                                   BindingFlags.Public);
            foreach (FieldInfo field in declaredFields)
            {
                try
                {
                    FieldInfo originField = sourceResource.GetType().GetField(field.Name, BindingFlags.Instance |
                                                                                   BindingFlags.Static |
                                                                                   BindingFlags.NonPublic |
                                                                                   BindingFlags.Public);
                    field.SetValue(destinationResource, originField.GetValue(sourceResource));

                }
                catch (Exception ex)
                {
                    throw new MPException(ex.Message);
                }
            }

            return destinationResource;
        }

        /// <summary>
        /// Calls the api and returns an MPApiResponse.
        /// </summary>
        /// <returns>A MPAPIResponse object with the results.</returns>
        public static MPAPIResponse CallAPI(
            HttpMethod httpMethod,
            string path,
            PayloadType payloadType,
            JObject payload,
            WebHeaderCollection colHeaders,
            bool useCache,
            int requestTimeout,
            int retries)
        {
            string cacheKey = httpMethod.ToString() + "_" + path;
            MPAPIResponse response = null;

            if (useCache)
            {
                response = MPCache.GetFromCache(cacheKey);

                if (response != null)
                {
                    response.IsFromCache = true;
                }
            }

            if (response == null)
            {
                response = new MPRESTClient().ExecuteRequest(
                    httpMethod,
                    path,
                    payloadType,
                    payload,
                    colHeaders,
                    requestTimeout,
                    retries);

                if (useCache)
                {
                    MPCache.AddToCache(cacheKey, response);
                }
                else
                {
                    MPCache.RemoveFromCache(cacheKey);
                }
            }

            return response;
        }

        /// <summary>
        /// Get the method we are searching on a specific class type.
        /// </summary>
        /// <param name="clazz">Type of class we are using.</param>
        /// <param name="methodName">Method we are trying to call.</param>
        /// <returns>Info about the method we are searching.</returns>
        private static MethodInfo GetAnnotatedMethod(Type clazz, String methodName)
        {
            foreach (MethodInfo method in clazz.GetMethods())
            {
                if (method.Name == methodName && method.GetCustomAttributes(false).Length > 0)
                {
                    return method;
                }
            }

            throw new MPException("No annotated method found");
        }

        /// <summary>
        /// Get rest information based on method info.
        /// </summary>
        /// <param name="element">MethodInfo containing information about the method we are trying to call.</param>
        /// <returns>Dictionary with custom information.</returns>
        private static Dictionary<string, object> GetRestInformation(MethodInfo element)
        {
            if (element.GetCustomAttributes(false).Length == 0)
            {
                throw new MPException("No rest method found");
            }

            Dictionary<string, object> hashAnnotation = new Dictionary<string, object>();
            foreach (Attribute annotation in element.GetCustomAttributes(false))
            {
                if (annotation is BaseEndpoint)
                {
                    if (string.IsNullOrEmpty(((BaseEndpoint)annotation).Path))
                    {
                        throw new MPException(string.Format("Path not found for {0} method", ((BaseEndpoint)annotation).HttpMethod.ToString()));
                    }
                }
                else
                {
                    throw new MPException("Not supported method found");
                }

                hashAnnotation = new Dictionary<string, object>();
                hashAnnotation.Add("method", ((BaseEndpoint)annotation).HttpMethod);
                hashAnnotation.Add("path", ((BaseEndpoint)annotation).Path);
                hashAnnotation.Add("instance", element.ReturnType.Name);
                hashAnnotation.Add("Header", element.ReturnType.GUID);
                hashAnnotation.Add("payloadType", ((BaseEndpoint)annotation).PayloadType);
                hashAnnotation.Add("requestTimeout", ((BaseEndpoint)annotation).RequestTimeout);
                hashAnnotation.Add("retries", ((BaseEndpoint)annotation).Retries);
            }

            return hashAnnotation;
        }
        #endregion

        #region Tracking Methods
        /// <summary>
        /// Get Type of a required class by it's string name.
        /// </summary>
        /// <param name="typeName">Class name.</param>
        /// <returns>Type of required class.</returns>
        public static Type GetTypeFromStack()
        {
            MethodBase methodBase = new StackTrace().GetFrame(2).GetMethod();
            var className = methodBase.DeclaringType.FullName;
            var type = Type.GetType(className);
            if (type != null) return type;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = a.GetType(className);
                if (type != null)
                    return type;
            }
            return null;
        }
        #endregion

        #region Core Utilities 
        /// <summary>
        /// Generates a final Path based on parameters in Dictionary and resource properties.
        /// </summary>
        /// <typeparam name="T">MPBase resource.</typeparam>
        /// <param name="path">Path we are processing.</param>
        /// <param name="mapParams">Collection of parameters that we will use to process the final path.</param>
        /// <param name="resource">Resource containing parameters values to include in the final path.</param>
        /// <returns>Processed path to call the API.</returns>
        public static string ParsePath<T>(string path, Dictionary<string, string> mapParams, T resource) where T : MPBase
        {
            StringBuilder result = new StringBuilder();
            bool search = !path.Contains(':') && mapParams != null && mapParams.Any();

            if (path.Contains(':'))
            {
                int paramIterator = 0;
                while (path.Contains(':'))
                {
                    result.Append(path.Substring(0, path.IndexOf(':')));
                    path = path.Substring(path.IndexOf(':') + 1);
                    string param = path;
                    if (path.Contains('/'))
                    {
                        param = path.Substring(0, path.IndexOf('/'));
                    }

                    string value = string.Empty;
                    if (paramIterator <= 2 &&
                            mapParams != null &&
                            !string.IsNullOrEmpty(mapParams[string.Format("param{0}", paramIterator.ToString())]))
                    {
                        value = mapParams[string.Format("param{0}", paramIterator.ToString())];
                    }
                    else if (mapParams != null &&
                         !string.IsNullOrEmpty(mapParams[param]))
                    {
                        value = mapParams[param];
                    }
                    else
                    {
                        if (resource != null)
                        {
                            var newResource = resource;
                            newResource._lastApiResponse = null;

                            JObject json = JObject.FromObject(newResource);

                            var jValuePC = json.GetValue(ToPascalCase(param));
 
                            if (jValuePC != null)
                            {
                                value = jValuePC.ToString();
                            }  

                        }
                    }

                    if (string.IsNullOrEmpty(value))
                    {
                        throw new MPException("No argument supplied/found for path argument");
                    }  
                    if (path.Contains('/'))
                    {
                        path = path.Substring(path.IndexOf('/'));
                    }
                    else
                    {
                        path = string.Empty;
                    }

                    result.Append(value);
                }

                if (!string.IsNullOrEmpty(path))
                {
                    result.Append(path);
                }
            }
            else
            {
                result.Append(path);
            }

            // URL
            result.Insert(0, SDK.BaseUrl);

            // Access Token
            string accessToken = SDK.GetAccessToken();

            if (!string.IsNullOrEmpty(accessToken))
            {
                result.Append(string.Format("{0}{1}", "?access_token=", accessToken));
            }

            if (search) //search url format, no :id type. Params after access_token
            {
                foreach (var elem in mapParams)
                {
                    result.Append(string.Format("{0}{1}={2}", "&", elem.Key, elem.Value));
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Gets the user custom token.
        /// </summary>
        /// <param name="value">Class type to retrieve the custom UserToken Attribute.</param>
        public static string GetUserToken(Type classType)
        {
            UserToken userTokenAttribute = null;
            var userToken = "";
            userTokenAttribute = ((UserToken)Attribute.GetCustomAttribute(classType, typeof(UserToken)));

            if (userTokenAttribute != null)
            {
                userToken = userTokenAttribute.GetUserToken();
            }

            return userToken;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public JObject GetJsonSource()
        {
            return _lastApiResponse != null ? _lastApiResponse.JsonObjectResponse : null;
        }

        #endregion

        #region Validation Methods

        private static WebHeaderCollection GetStandardHeaders()
        {
            WebHeaderCollection colHeaders = new WebHeaderCollection();
            colHeaders.Add("HTTP.CONTENT_TYPE: application/json");
            colHeaders.Add("HTTP.USER_AGENT: MercadoPago Java SDK v1.0.1");
            return colHeaders;
        }

        #endregion

        #region Testing helpers

        

        public static string ToPascalCase(string text)
        {
            const string pattern = @"(-|_)\w{1}|^\w";
            return Regex.Replace(text, pattern, match => match.Value.Replace("-", string.Empty).Replace("_", string.Empty).ToUpper());
        }
        public static string ToSnakeCase(string text){
            const string pattern = @"(?<=[a-z0-9])[A-Z\s]";
            return Regex.Replace(text, pattern, "_$0").ToLower();
        }
        #endregion
    }
}
