using System.Net;
using System;
using Mailjet.Client;
using Mailjet.Client.Resources;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
const string separate_symbol = ",";
            MailContents mail = new MailContents();
            bool hasError = false;
            string errorMessage = string.Empty;

            dynamic data = await req.Content.ReadAsAsync<object>();
            mail.mjAPI_PubK = data?.MailJetAPI_PublicKey ?? string.Empty;
            mail.mjAPI_PriK = data?.MailJetAPI_PrivateKey ?? string.Empty;
            mail.senderMail = data?.SenderMail ?? string.Empty;
            mail.senderName = data?.SenderName ?? string.Empty;
            mail.subject = data?.Subject ?? string.Empty;
            mail.body_txt = data?.Body_TextPlain ?? string.Empty;
            mail.body_html = data?.Body_HTML ?? string.Empty;
            mail.TempleteID = data?.TempleteID ?? string.Empty;

            if(data?.Template_Variables != null && !string.IsNullOrEmpty(data?.Template_Variables.ToString()))
            {
                string temp = data?.Template_Variables.ToString();
                mail.Template_Variables = JsonConvert.DeserializeObject<Dictionary<string, string>>(temp) ?? null;
            }

            if (data?.To != null)
                mail.MailTo = ConvertRequestValue(data?.To.ToString());
            if (data?.Cc != null)
                mail.MailCc = ConvertRequestValue(data?.Cc.ToString());
            if (data?.Bcc != null)
                mail.MailBcc = ConvertRequestValue(data?.Bcc.ToString());
            if (data?.Attachments_URL != null)
                mail.Attachment_URL = ConvertRequestValue(data?.Attachments_URL.ToString());

            try
            {
                //Checked require fields.
                if(string.IsNullOrEmpty(mail.mjAPI_PubK) || string.IsNullOrEmpty(mail.mjAPI_PriK) || string.IsNullOrEmpty(mail.senderMail)
                    || string.IsNullOrEmpty(mail.senderName) || string.IsNullOrEmpty(mail.MailTo))
                {
                    string er = "MailJet API Public Key, MailJet API Private Key, Sender mail, Sender name, and Recipients are required.";
                    throw new Exception(er);
                }

                JObject template_vars = null;

                if(mail.Template_Variables != null && mail.Template_Variables.Count > 0)
                {
                    template_vars = new JObject();
                    foreach(KeyValuePair<string,string> dic in mail.Template_Variables)
                    {
                        template_vars.Add(dic.Key, dic.Value);
                    }
                }

                JObject[] attachmentMails = null;

                if (!string.IsNullOrEmpty(mail.Attachment_URL))
                {
                    string[] attachments = mail.Attachment_URL.Split(Convert.ToChar(separate_symbol));

                    attachmentMails = new JObject[attachments.Length];

                    for (int i = 0; i < attachments.Length; i++)
                    {
                        string file_name = string.Empty;
                        string fileName = string.Empty;
                        string content = string.Empty;
                        string contentType = string.Empty;

                        using (WebClient wc = new WebClient())
                        {
                            string uri = attachments[i];
                            string[] uri_content = uri.Split('/');
                            //Get file name from URL.
                            fileName = uri_content[uri_content.Length - 1];
                            byte[] file_content = wc.DownloadData(uri);
                            content = Convert.ToBase64String(file_content);

                            var _re = HttpWebRequest.Create(uri) as HttpWebRequest;
                            if (_re != null)
                            {
                                var _rep = _re.GetResponse() as HttpWebResponse;

                                if (_rep != null)
                                    contentType = _rep.ContentType;
                            }
                        }

                        attachmentMails[i] = new JObject {{"Content-Type", contentType},
                                          {"Filename",fileName},
                                          {"content",content}};
                    }


                }

                MailjetClient client = new MailjetClient(mail.mjAPI_PubK, mail.mjAPI_PriK);
                MailjetRequest request = new MailjetRequest
                {
                    Resource = Send.Resource,
                }
                .Property(Send.FromEmail, mail.senderMail)
                .Property(Send.FromName, mail.senderName)
                .Property(Send.Subject, mail.subject)
                .Property(Send.TextPart, mail.body_txt)
                .Property(Send.HtmlPart, mail.body_html)
                .Property(Send.MjTemplateID, mail.TempleteID)
                .Property(Send.MjTemplateLanguage, "True")
                .Property(Send.To, mail.MailTo)
                .Property(Send.Cc, mail.MailCc)
                .Property(Send.Bcc, mail.MailBcc);

                if(template_vars != null && template_vars.Count > 0)
                    request.Property(Send.Vars, template_vars);

                if (attachmentMails != null && attachmentMails.Length > 0)
                    request.Property(Send.Attachments, new JArray(attachmentMails));

                MailjetResponse response = await client.PostAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    log.Info(string.Format($"Total: {response.GetTotal()}, Count: {response.GetCount()}\n"));
                    log.Info(response.GetData().ToString());
                    hasError = false;
                }
                else
                {
                    log.Info(string.Format("StatusCode: {0}\n", response.StatusCode));
                    log.Info(string.Format("ErrorInfo: {0}\n", response.GetErrorInfo()));
                    log.Info(string.Format("ErrorMessage: {0}\n", response.GetErrorMessage()));

                    hasError = true;

                    errorMessage = $"StatusCode: {response.StatusCode} \r\n";
                    errorMessage += $"ErrorInfo: {response.GetErrorInfo()} \r\n";
                    errorMessage += $"ErrorMessage: {response.GetErrorMessage()}";
                }
            }
            catch (Exception ex)
            {
                hasError = true;
                errorMessage = ex.Message;
            }

            return hasError
                ? req.CreateResponse(HttpStatusCode.BadRequest, $"Sending mail fail!! \r\n{errorMessage}")
                : req.CreateResponse(HttpStatusCode.OK, "Sending mail success.");
        }

        public class MailContents
        {
            public string mjAPI_PubK { get; set; }
            public string mjAPI_PriK { get; set; }
            public string senderMail { get; set; }
            public string senderName { get; set; }
            public string subject { get; set; }
            public string body_txt { get; set; }
            public string body_html { get; set; }
            public string MailTo { get; set; }
            public string MailCc { get; set; }
            public string MailBcc { get; set; }
            public string Attachment_URL { get; set; }
            public string TempleteID { get; set; }
            public Dictionary<string,string> Template_Variables { get; set; }
        }

        public static string ConvertRequestValue(string input)
        {
            if (string.IsNullOrEmpty(input))
                input = string.Empty;

            return input.Replace("\r\n", string.Empty).Replace("\"", string.Empty).Replace("[", string.Empty).Replace("]", string.Empty).Replace(" ", string.Empty);
        }
