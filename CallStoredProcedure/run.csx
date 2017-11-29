#r "System.Configuration"
#r "System.Data"
#r "Newtonsoft.Json"

using System.Net;
using System.Text;
using System.Configuration;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
            List<ColumnDetail> lstParamIn = new List<ColumnDetail>();
            List<ColumnDetail> lstParamOut = new List<ColumnDetail>();

            string returnColName = string.Empty;
            HttpStatusCode statusCode = 0;
            string errorMessage = string.Empty;
            int returnValue = 0;
            const string sp = "   ";
            const string dateTimeFormat = "yyyy-MM-ddThh:mm:ss";

            StringBuilder temp = new StringBuilder();
            StringBuilder ret = new StringBuilder();
            List<string> d = new List<string>();
            bool hasError = false;

            string connectionString = string.Empty;
            string storedP = string.Empty;
            string connStrP = string.Empty;
            string connNameP = string.Empty;

            dynamic param_s = null;
            dynamic data = null;

            data = await req.Content.ReadAsAsync<object>();
            connStrP = data["ConnectionString"];
            connNameP = data["ConnectionName"];
            storedP = data["StoredProcedureName"];

            if (!string.IsNullOrEmpty(connStrP))
                connectionString = connStrP;
            else if (!string.IsNullOrEmpty(connNameP))
                connectionString = ConfigurationManager.ConnectionStrings[connNameP].ConnectionString;
            else
                connectionString = string.Empty;

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                try
                {
                    using (SqlCommand cmd = new SqlCommand(storedP, conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        SqlCommandBuilder.DeriveParameters(cmd);

                        //Get input and output column in StoredProcedure.
                        GetParameterColumn(ref lstParamIn, ref lstParamOut, ref returnColName, cmd.Parameters);

                        param_s = data["Parameters"];

                        int i = 0;
                        if (param_s != null)
                        {
                            foreach (ColumnDetail obj in lstParamIn)
                            {
                                string colName = obj.ColumnName.Replace("@", string.Empty);
                                object val = null;
                                val = param_s[colName];

                                if (val != null)
                                {
                                    cmd.Parameters[obj.ColumnName].Value = ConvertSqldata(obj.ColumnType, val);
                                }

                                i++;
                            }
                        }
                        
                        DataSet ds = new DataSet();
                        SqlDataAdapter resultAdapter = new SqlDataAdapter(cmd);
                        resultAdapter.Fill(ds);

                        int tableNum = 0;
                        foreach (DataTable dt in ds.Tables)
                        {
                            d.Add($"{sp}\"Table{tableNum + 1}\": [");

                            int countRow = 0;
                            foreach (DataRow row in dt.Rows)
                            {
                                d.Add($"{sp}{sp}" + "{");

                                String tmpMes = "";

                                int countCol = 0;
                                foreach (DataColumn column in dt.Columns)
                                {
                                    tmpMes = $"{sp}{sp}{sp}\"{column.ColumnName}\": ";

                                    row[column] = row[column] == null ? string.Empty : row[column];

                                    switch (column.DataType.ToString())
                                    {
                                        case "System.String":
                                            if (!string.IsNullOrEmpty(row[column].ToString()))
                                                tmpMes += "\"" + row[column].ToString().Trim() + "\"";
                                            else
                                                tmpMes += "\"NULL\"";
                                            break;
                                        case "System.DateTime":
                                            if (!string.IsNullOrEmpty(row[column].ToString()))
                                                tmpMes += "\"" + Convert.ToDateTime(row[column].ToString().Trim()).ToString(dateTimeFormat) + "\"";
                                            else
                                                tmpMes += "\"NULL\"";
                                            break;
                                        case "System.Boolean":
                                            if (!string.IsNullOrEmpty(row[column].ToString()))
                                                tmpMes += Convert.ToBoolean(row[column].ToString().Replace("F", "f").Replace("T", "t"));
                                            else
                                                tmpMes += "\"NULL\"";
                                            break;
                                        case "System.Byte[]":
                                            if (!string.IsNullOrEmpty(row[column].ToString()))
                                                tmpMes += BitConverter.ToString((byte[])row[column]);
                                            else
                                                tmpMes += "\"NULL\"";
                                            break;
                                        default:
                                            if (!string.IsNullOrEmpty(row[column].ToString()))
                                                tmpMes += row[column].ToString();
                                            else
                                                tmpMes += "\"NULL\"";
                                            break;
                                    }

                                    countCol++;
                                    if (countCol == dt.Columns.Count)
                                        d.Add(tmpMes);
                                    else
                                        d.Add(tmpMes + ",");


                                }

                                countRow++;
                                if (countRow == dt.Rows.Count)
                                    d.Add($"{sp}{sp}" + "}");
                                else
                                    d.Add($"{sp}{sp}" + "},");
                            }

                            tableNum++;
                            if (tableNum == ds.Tables.Count)
                                d.Add($"{sp}]");
                            else
                                d.Add($"{sp}],");
                        }

                        i = 0;
                        foreach (ColumnDetail obj in lstParamOut)
                        {
                            lstParamOut[i].ColumnValue = cmd.Parameters[obj.ColumnName].Value == null ? "" : cmd.Parameters[obj.ColumnName].Value.ToString().Trim();
                            i++;
                        }

                        returnValue = cmd.Parameters[returnColName].Value == null ? 0 : (int)cmd.Parameters[returnColName].Value;

                    }

                }
                catch (SqlException ex)
                {
                    statusCode = HttpStatusCode.OK;
                    returnValue = ex.Number;
                    errorMessage = ex.Message;
                    hasError = false;
                }
                catch (Exception ex)
                {
                    statusCode = HttpStatusCode.BadRequest;
                    errorMessage = ex.Message;
                    returnValue = -1;
                    hasError = true;
                }
                finally
                {
                    if (!hasError)
                        statusCode = HttpStatusCode.OK;
                }

                conn.Close();
            }
            temp.AppendLine($"" + "{");
            temp.AppendLine($"\"OutputParameters\": " + "{");
            if(returnValue == 0){
                bool isInsert = false;
                foreach (ColumnDetail obj in lstParamOut)
                {
                    if (isInsert) temp.AppendLine(",");

                    isInsert = true;
                    if (obj.ColumnType == SqlDbType.DateTime)
                    {
                        temp.Append($"{sp}\"{obj.ColumnName.Replace("@", string.Empty)}\": { Convert.ToDateTime(obj.ColumnValue.ToString()).ToString(dateTimeFormat)}");
                    }
                    else
                    {
                        temp.Append($"{sp}\"{obj.ColumnName.Replace("@", string.Empty)}\": {ConvertSQLDataToDisplay(obj.ColumnType, obj.ColumnValue)}");
                    }
                }
            }
            
            temp.AppendLine($"" + "},");
            temp.AppendLine($"\"ReturnCode\": {returnValue},");
            temp.AppendLine($"\"ErrorMessage\": \"{errorMessage}\",");
            temp.AppendLine($"\"ResultSets\": " + "{");
            if(returnValue == 0){
                foreach (string read in d)
                {
                    temp.AppendLine(read);
                }
            }
            temp.AppendLine($"" + "}");
            temp.AppendLine($"" + "}");
            var json = JsonConvert.SerializeObject(temp.ToString(), Formatting.Indented);

            return hasError
            ? new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        public static void GetParameterColumn(ref List<ColumnDetail> pIn, ref List<ColumnDetail> pOut, ref string returnColName, SqlParameterCollection param)
        {
            foreach (SqlParameter SP_param in param)
            {
                ColumnDetail obj = new ColumnDetail();
                obj.ColumnName = SP_param.ParameterName == null ? string.Empty : SP_param.ParameterName;
                obj.ColumnValue = ConvertSqldata(SP_param.SqlDbType, SP_param.Value);
                obj.ColumnType = SP_param.SqlDbType;
                obj.ColumnTypeSize = SP_param.Size;

                switch (SP_param.Direction)
                {
                    case ParameterDirection.Input:
                        pIn.Add(obj);
                        break;
                    case ParameterDirection.InputOutput:
                        pOut.Add(obj);
                        pIn.Add(obj);
                        break;
                    case ParameterDirection.ReturnValue:
                        returnColName = SP_param.ParameterName;
                        break;
                }
            }
        }

        public static string ConvertSQLDataToDisplay(SqlDbType type, object inputValue)
        {
            string result = string.Empty;

            switch (type)
            {
                case SqlDbType.Int:
                case SqlDbType.BigInt:
                case SqlDbType.Bit:
                case SqlDbType.Real:
                case SqlDbType.Decimal:
                case SqlDbType.Float:
                case SqlDbType.Money:
                case SqlDbType.SmallInt:
                case SqlDbType.SmallMoney:
                case SqlDbType.TinyInt:
                    result = $"{inputValue.ToString()}";
                    break;
                case SqlDbType.Char:
                case SqlDbType.NChar:
                case SqlDbType.VarChar:
                case SqlDbType.NVarChar:
                case SqlDbType.Text:
                case SqlDbType.NText:
                case SqlDbType.Date:
                case SqlDbType.DateTime:
                case SqlDbType.DateTime2:
                case SqlDbType.DateTimeOffset:
                case SqlDbType.SmallDateTime:
                case SqlDbType.Time:
                case SqlDbType.Timestamp:
                    result = result = $"\"{inputValue.ToString()}\"";
                    break;
                default:
                    result = $"\"{inputValue.ToString()}\"";
                    break;
            }

            return result;
        }

        public static object ConvertSqldata(SqlDbType type, object inputValue)
        {
            object result = null;

            switch (type)
            {
                case SqlDbType.Int: //********************
                case SqlDbType.BigInt:
                    result = inputValue == null ? -1 : Convert.ToInt32(inputValue);
                    break;
                case SqlDbType.Bit:
                    result = inputValue == null ? string.Empty : Convert.ToBoolean(inputValue).ToString();
                    break;
                case SqlDbType.Real:
                case SqlDbType.Float:
                    result = inputValue == null ? 0 : Convert.ToSingle(inputValue.ToString());
                    break;
                case SqlDbType.Decimal: //***************
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    result = inputValue == null ? 0 : Convert.ToDecimal(inputValue.ToString());
                    break;
                case SqlDbType.SmallInt:
                    result = inputValue == null ? 0 : Convert.ToInt16(inputValue.ToString());
                    break;
                case SqlDbType.TinyInt:
                    result = inputValue == null ? Convert.ToByte(inputValue) : Convert.ToByte(inputValue);
                    break;
                case SqlDbType.Char:
                case SqlDbType.NChar:
                case SqlDbType.VarChar: // ****************************
                case SqlDbType.NVarChar:
                case SqlDbType.Text:
                case SqlDbType.NText:
                    result = inputValue == null ? string.Empty : Convert.ToString(inputValue);
                    break;
                case SqlDbType.Date:
                case SqlDbType.DateTime: //********************************
                case SqlDbType.DateTime2:
                case SqlDbType.SmallDateTime:
                    result = inputValue == null ? DateTime.MinValue : Convert.ToDateTime(inputValue.ToString());
                    break;
                case SqlDbType.DateTimeOffset:
                    result = inputValue == null ? DateTimeOffset.MinValue : DateTimeOffset.Parse(inputValue.ToString());
                    break;
                case SqlDbType.Time:
                    result = inputValue == null ? TimeSpan.MinValue : TimeSpan.Parse(inputValue.ToString());
                    break;
                case SqlDbType.Timestamp: //******************************
                    result = inputValue == null ? new byte[255] : Encoding.ASCII.GetBytes(inputValue.ToString());
                    break;
                default:
                    result = inputValue == null ? string.Empty : Convert.ToString(inputValue);
                    break;
            }

            return result;
        }

        public class ColumnDetail
        {
            public string ColumnName { get; set; }
            public object ColumnValue { get; set; }
            public SqlDbType ColumnType { get; set; }
            public int ColumnTypeSize { get; set; }
        }