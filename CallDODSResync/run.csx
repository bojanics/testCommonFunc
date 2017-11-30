#r "Newtonsoft.Json"

using System.Net;
using System.Net.Http;
using System;
using Newtonsoft.Json.Linq;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
            string dods_url = string.Empty;
            string dbName = string.Empty;
            string className = string.Empty;
            string oid = string.Empty;
            string request_url = string.Empty;
            string userName = string.Empty;
            string password = string.Empty;
            HttpStatusCode statusCode = HttpStatusCode.OK;
            string statusMessage = string.Empty;
            string DODSCacheResyncReport = string.Empty;

            try
            {
                // Get request body
                dynamic data = await req.Content.ReadAsAsync<object>();

                dods_url = data?.DODS_Resync_URL ?? dods_url;
                dbName = data?.DB_Name ?? dbName;
                className = data?.Class_Name ?? className;
                oid = data?.OID ?? oid;
                userName = data?.Web_Server_UserName ?? userName;
                password = data?.Web_Server_Password ?? password;

                if (!string.IsNullOrEmpty(dods_url) &&
                    !string.IsNullOrEmpty(dbName) &&
                    !string.IsNullOrEmpty(className) &&
                    !string.IsNullOrEmpty(oid))
                {
                    request_url = dods_url.Trim();
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
                    throw new Exception("DODS_Resync_URL, DB_Name, Class_Name and OID are required.");
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
                    statusMessage = statusCode.ToString() + ". " + webex.Message;
                }
            }
            catch (Exception ex)
            {
                statusCode = HttpStatusCode.InternalServerError;
                statusMessage = ex.Message;
            }

            int code = (int)statusCode;
            return req.CreateResponse(statusCode, new JObject{
                { "statusCode", code },
                {"statusMessage", statusMessage},
                { "DODSCacheResyncReport", DODSCacheResyncReport}
            });

        } 
