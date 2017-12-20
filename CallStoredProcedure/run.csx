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
using Newtonsoft.Json.Linq;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
            List<ColumnDetail> lstParamIn = new List<ColumnDetail>();
            List<ColumnDetail> lstParamOut = new List<ColumnDetail>();

            string returnColName = string.Empty;
            HttpStatusCode statusCode = 0;
            string _null = string.Empty;
            string errorMessage = string.Empty;
            int returnValue = 0;
            const string dateTimeFormat = "yyyy-MM-ddThh:mm:ss";

            StringBuilder temp = new StringBuilder();
            StringBuilder ret = new StringBuilder();
            List<string> d = new List<string>();
            bool hasError = false;
            JObject response_body = new JObject();
            JObject _table = new JObject();

            dynamic param_s = null;
            dynamic data = null;
            SqlConnection conn = null;

            try
            {

                data = await req.Content.ReadAsAsync<object>();
                string connectionString = data?.ConnectionString ?? string.Empty;
                string connectionString_app_setting = data?.ConnectionString_AppSettingName ?? string.Empty;
                string stored_prcedure_name = data?.StoredProcedureName ?? string.Empty;

                if (string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(connectionString_app_setting))
                    connectionString = System.Environment.GetEnvironmentVariable("DEFAULT_ConnectionString");
                else if (string.IsNullOrEmpty(connectionString) && !string.IsNullOrEmpty(connectionString_app_setting))
                    connectionString = System.Environment.GetEnvironmentVariable(connectionString_app_setting);

                if (string.IsNullOrEmpty(connectionString))
                    throw new Exception("Connection String is missing.");

                using (conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                
                        using (SqlCommand cmd = new SqlCommand(stored_prcedure_name, conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;

                            SqlCommandBuilder.DeriveParameters(cmd);

                            //Get input and output column in StoredProcedure.
                            GetParameterColumn(ref lstParamIn, ref lstParamOut, ref returnColName, cmd.Parameters);

                            param_s = data["Parameters"];

                            int i = 0;
                            if (param_s != null && cmd.Parameters.Count > 0)
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

                            if (cmd.Parameters.Count > 0)
                            {
                                for (i = 0; i < cmd.Parameters.Count; i++)
                                {
                                    if (cmd.Parameters[i].Direction == ParameterDirection.InputOutput && cmd.Parameters[i].Value == null)
                                        cmd.Parameters[i].Direction = ParameterDirection.Output;
                                }
                            }

                            DataSet ds = new DataSet();
                            SqlDataAdapter resultAdapter = new SqlDataAdapter(cmd);
                            resultAdapter.Fill(ds);

                            int tableNum = 0;

                            foreach (DataTable dt in ds.Tables)
                            {
                                JArray _table_ar = new JArray();
                                foreach (DataRow row in dt.Rows)
                                {
                                    JObject _row = new JObject();
                                    foreach (DataColumn column in dt.Columns)
                                    {
                                        row[column] = row[column] == null ? string.Empty : row[column];

                                        switch (column.DataType.ToString())
                                        {
                                            case "System.String":
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, row[column].ToString().Trim());
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                            case "System.DateTime":
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, Convert.ToDateTime(row[column].ToString().Trim()).ToString(dateTimeFormat));
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                            case "System.Boolean":
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, Convert.ToBoolean(row[column].ToString().Replace("F", "f").Replace("T", "t")));
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                            case "System.Byte[]":
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, BitConverter.ToString((byte[])row[column]));
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                            case "System.Int32":
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, int.Parse(row[column].ToString()));
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                            case "System.Decimal":
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, decimal.Parse(row[column].ToString()));
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                            default:
                                                if (!string.IsNullOrEmpty(row[column].ToString()))
                                                    _row.Add(column.ColumnName, row[column].ToString());
                                                else
                                                    _row.Add(column.ColumnName, _null);
                                                break;
                                        }
                                    }

                                    _table_ar.Add(_row);
                                }

                                tableNum++;

                                _table.Add($"Table{tableNum}", _table_ar);
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
                statusCode = HttpStatusCode.InternalServerError;
                errorMessage = ex.Message;
                returnValue = -1;
                hasError = true;
            }
            finally
            {
                if (!hasError)
                    statusCode = HttpStatusCode.OK;

                if(conn != null)
                    conn.Close();
            }

            JObject _outP = new JObject();
            if (returnValue == 0)
            {
                foreach (ColumnDetail obj in lstParamOut)
                {
                    obj.ColumnName = obj.ColumnName.Replace("@", string.Empty);
                    switch (obj.ColumnType)
                    {
                        case SqlDbType.Int:
                            if (!string.IsNullOrEmpty(obj.ColumnValue.ToString()))
                                _outP.Add(obj.ColumnName, int.Parse(obj.ColumnValue.ToString()));
                            else
                                _outP.Add(obj.ColumnName, _null);
                            break;
                        case SqlDbType.Decimal:
                        case SqlDbType.Float:
                            if (!string.IsNullOrEmpty(obj.ColumnValue.ToString()))
                                _outP.Add(obj.ColumnName, decimal.Parse(obj.ColumnValue.ToString()));
                            else
                                _outP.Add(obj.ColumnName, _null);
                            break;
                        case SqlDbType.Timestamp:
                            if (!string.IsNullOrEmpty(obj.ColumnValue.ToString()))
                                _outP.Add(obj.ColumnName, BitConverter.ToString((byte[])obj.ColumnValue));
                            else
                                _outP.Add(obj.ColumnName, _null);
                            break;
                        case SqlDbType.Bit:
                            if (!string.IsNullOrEmpty(obj.ColumnValue.ToString()))
                                _outP.Add(obj.ColumnName, Convert.ToBoolean(obj.ColumnValue.ToString().Replace("F", "f").Replace("T", "t")));
                            else
                                _outP.Add(obj.ColumnName, _null);
                            break;
                        case SqlDbType.DateTime:
                            if (!string.IsNullOrEmpty(obj.ColumnValue.ToString()))
                                _outP.Add(obj.ColumnName, Convert.ToDateTime(obj.ColumnValue.ToString().Trim()).ToString(dateTimeFormat));
                            else
                                _outP.Add(obj.ColumnName, _null);
                            break;
                        default:
                            _outP.Add(obj.ColumnName, (string)obj.ColumnValue);
                            break;
                    }

                }
            }
            response_body.Add("OutputParameters", _outP);
            response_body.Add("ReturnCode", returnValue);
            response_body.Add("ErrorMessage", errorMessage);
            response_body.Add("ResultSets", _table);

            return req.CreateResponse(statusCode, response_body);
            
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

        public static object ConvertSqldata(SqlDbType type, object inputValue)
        {
            object result = null;

            if (inputValue != null && string.IsNullOrEmpty(inputValue.ToString()))
                inputValue = null;

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
                    result = inputValue == null ? -999 : Convert.ToSingle(inputValue.ToString());
                    break;
                case SqlDbType.Decimal: //***************
                case SqlDbType.Money:
                case SqlDbType.SmallMoney:
                    result = inputValue == null ? -999 : Convert.ToDecimal(inputValue.ToString());
                    break;
                case SqlDbType.SmallInt:
                    result = inputValue == null ? -999 : Convert.ToInt16(inputValue.ToString());
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