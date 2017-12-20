#r "Newtonsoft.Json"

using System.Net;
using System.Net.Http;
using System;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
            string request_url = string.Empty; 
            HttpStatusCode statusCode = HttpStatusCode.OK;  
            string statusMessage = string.Empty;
            string DODSCacheResyncReport = string.Empty;
            const string default_dods_url = "DEFAULT_DODS_Resync_URL";
            const string default_dbName = "DEFAULT_DB_Name"; 
            const string default_server_userName = "DEFAULT_Web_Server_UserName";
            const string default_server_password = "DEFAULT_Web_Server_Password";
            bool isDefaultSettingError = false;
            List<string> default_app_setting_error = new List<string>();

            try
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();

                string dods_resynce_url = data?.DODS_Resync_URL ?? string.Empty;
                string dods_resynce_url_app_setting = data?.DODS_Resync_URL_AppSettingName ?? string.Empty;
                string dbName = data?.DB_Name ?? string.Empty;
                string dbName_app_setting = data?.DB_Name_AppSettingName ?? string.Empty;
                string className = data?.Class_Name ?? string.Empty;
                string oid = data?.OID ?? string.Empty;
                string userName = data?.Web_Server_UserName ?? string.Empty;
                string userName_app_setting = data?.Web_Server_UserName_AppSettingName ?? string.Empty;
                string password = data?.Web_Server_Password ?? string.Empty;
                string password_app_setting = data?.Web_Server_Password_AppSettingName ?? string.Empty;

                if (string.IsNullOrEmpty(dods_resynce_url) && string.IsNullOrEmpty(dods_resynce_url_app_setting))
                    dods_resynce_url = System.Environment.GetEnvironmentVariable(default_dods_url);
                else if (string.IsNullOrEmpty(dods_resynce_url) && !string.IsNullOrEmpty(dods_resynce_url_app_setting))
                    dods_resynce_url = System.Environment.GetEnvironmentVariable(dods_resynce_url_app_setting);

                if (string.IsNullOrEmpty(dbName) && string.IsNullOrEmpty(dbName_app_setting))
                    dbName = System.Environment.GetEnvironmentVariable(default_dbName);
                else if (string.IsNullOrEmpty(dbName) && !string.IsNullOrEmpty(dbName_app_setting))
                    dbName = System.Environment.GetEnvironmentVariable(dbName_app_setting);

                if (string.IsNullOrEmpty(userName) && string.IsNullOrEmpty(userName_app_setting))
                    userName = System.Environment.GetEnvironmentVariable(default_server_userName);
                else if (string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(userName_app_setting))
                    userName = System.Environment.GetEnvironmentVariable(userName_app_setting);

                if (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(password_app_setting))
                    password = System.Environment.GetEnvironmentVariable(default_server_password);
                else if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(password_app_setting))
                    password = System.Environment.GetEnvironmentVariable(password_app_setting);

                if (string.IsNullOrEmpty(dods_resynce_url))
                {
                    isDefaultSettingError = true;
                    default_app_setting_error.Add("DODS_Resync_URL");
                }

                if (string.IsNullOrEmpty(dbName))
                {
                    isDefaultSettingError = true;
                    default_app_setting_error.Add("DB_Name");
                    
                }

                if (isDefaultSettingError)
                    throw new Exception(string.Empty);

                if (
                    !string.IsNullOrEmpty(className) &&
                    !string.IsNullOrEmpty(oid))
                {
                    request_url = dods_resynce_url.Trim();
                    request_url += "?dbname=" + dbName.Trim();
                    request_url += "&classname=" + className.Trim();
                    request_url += "&oid=" + oid.Trim();

                    var uri = new Uri(request_url);
                    var _re = HttpWebRequest.Create(uri) as HttpWebRequest;

                    if (!string.IsNullOrEmpty(userName) && !string.IsNullOrEmpty(password))
                    {
                        NetworkCredential myCred = new NetworkCredential(userName, password);
                        CredentialCache credsCache = new CredentialCache();
                        credsCache.Add(uri, "Basic", myCred);
                        _re.Credentials = credsCache;
                    }

                    if (_re != null)
                    {
                        var _rep = _re.GetResponse() as HttpWebResponse;

                        if (_rep != null)
                        {
                            statusCode = _rep.StatusCode;
                            statusMessage = (int)_rep.StatusCode + " " + _rep.StatusDescription;
                            DODSCacheResyncReport = _rep.GetResponseHeader("DODSCacheResyncReport");
                        }
                    }
                }
                else
                {
                    throw new Exception("Class_Name and OID are required.");
                }
            }
            catch (WebException webex)
            {
                HttpWebResponse response;
                if (webex.Response is HttpWebResponse)
                {
                    response = (HttpWebResponse)webex.Response;
                    statusCode = response.StatusCode;
                    statusMessage = response.StatusDescription;
                    DODSCacheResyncReport = response.GetResponseHeader("DODSCacheResyncReport");
                }
                else
                {
                    statusCode = HttpStatusCode.NotFound;
                    statusMessage = webex.Message;
                }
            }
            catch (Exception ex)
            {
                statusCode = HttpStatusCode.InternalServerError;

                if (isDefaultSettingError)
                {
                    string msg = string.Empty;
                    for(int i=0;i<default_app_setting_error.Count; i++)
                    {
                        if (i > 0 && i == default_app_setting_error.Count - 1)
                            msg += " and ";
                        else if (i > 0)
                            msg += ", ";

                        msg += default_app_setting_error[i];
                    }

                    if (default_app_setting_error.Count > 1)
                        msg += " are missing.";
                    else
                        msg += " is missing.";

                    statusMessage = msg;
                }
                else
                {
                    statusMessage = ex.Message;
                }
            }

            int code = (int)statusCode;
            return req.CreateResponse(statusCode, new JObject{
                { "statusCode", code },
                {"statusMessage", statusMessage},
                { "DODSCacheResyncReport", DODSCacheResyncReport}
            });

        }
