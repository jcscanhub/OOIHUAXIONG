// See https://aka.ms/new-console-template for more information

using JCScanhubClockInSystem;
using JCScanhubClockInSystem.Model;
using System;
using System.Configuration;
using System.Data.SqlClient;
using Newtonsoft.Json; // Make sure to add this using directive
using System.Text.RegularExpressions;
using Dapper;

class Program
{
    static async Task Main(string[] args)
    {

        // Replace the following with your actual values
        string serverAddress1 = "http://192.168.1.108";
        string resourcePath1 = $"/cgi-bin/recordFinder.cgi?action=find&name=AccessControlCardRec&StartTime={TimeUtility.GetTodayStartTimestamp()}&EndTime={TimeUtility.GetTodayEndTimestamp()}";
        string serialNoPath = "/cgi-bin/magicBox.cgi?action=getSerialNo";
        string deviceModelPath = "/cgi-bin/magicBox.cgi?action=getDeviceType";

        string username = "admin";
        string password = "Welcome2024";

        //var result = await apiClient.ProcessApiRequestWithDigestAuthAsync(serverAddress1, resourcePath1, username, password);

        DigestAuthFixer digest = new DigestAuthFixer(serverAddress1, username, password);
        string strReturn = digest.GrabResponse(resourcePath1);

        string getSerialNo = digest.GrabResponse(serialNoPath);
        string snValue = ExtractValue(getSerialNo);

        string getDeviceModel = digest.GrabResponse(deviceModelPath);
        string snValue1 = ExtractValue(getDeviceModel);

        Dictionary<string, object> parsedData = ParseData(strReturn);
        int found = 0;
        if (parsedData.ContainsKey("found"))
        {
            found = Convert.ToInt32(parsedData["found"]);
        }
        Console.WriteLine("\nContains Records: " + ContainsRecords(parsedData));
        if (ContainsRecords(parsedData))
        {
            List<UnlockingRecords> records = ParseRecords(parsedData);
            if (found > 0)
            {

                // Filter records with Status == 1
                var filteredRecords = records.Where(record => record.Status == 1).ToList();

                if (filteredRecords.Count > 0)
                {
                    string connectionString = ConfigurationManager.ConnectionStrings["SqlAuthenticationConnection"].ConnectionString;

                    // Use the connection string as needed, for example, creating a SqlConnection
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        try
                        {
                            connection.Open();

                            string insertQuery = @"
                            INSERT INTO AttendanceRecord 
                            (AttendanceState, CardName, CardNo, CardType, CreateTime, Door, ErrorCode, FaceIndex, 
                             HatColor, HatType, Mask, Method, Notes, [Password], ReaderID, RecNo, RemainingTimes, 
                             ReservedInt, ReservedString, RoomNumber, [Status], [Type], [URL], UserID, UserType,
                            IPAddress, SerialNo, DeviceModel) 
                            VALUES 
                            (@AttendanceState, @CardName, @CardNo, @CardType, @CreateTime, @Door, @ErrorCode, @FaceIndex, 
                             @HatColor, @HatType, @Mask, @Method, @Notes, @Password, @ReaderID, @RecNo, @RemainingTimes, 
                             @ReservedInt, @ReservedString, @RoomNumber, @Status, @Type, @URL, @UserID, @UserType,
                             @IPAddress, @SerialNo, @DeviceModel)";

                            // Create and execute the parameterized query for all records
                            int rowsAffected = await connection.ExecuteAsync(insertQuery, filteredRecords.Select(record => new
                            {
                                record.AttendanceState,
                                record.CardName,
                                record.CardNo,
                                record.CardType,
                                CreateTime = UnixTimeStampToDateTime(record.CreateTime).ToString("yyyy/MM/dd HH:mm:ss"), // Include time in the conversion
                                record.Door,
                                record.ErrorCode,
                                record.FaceIndex,
                                record.HatColor,
                                record.HatType,
                                record.Mask,
                                record.Method,
                                record.Notes,
                                record.Password,
                                record.ReaderID,
                                record.RecNo,
                                record.RemainingTimes,
                                record.ReservedInt,
                                record.ReservedString,
                                record.RoomNumber,
                                record.Status,
                                record.Type,
                                record.URL,
                                record.UserID,
                                record.UserType,
                                IPAddress = serverAddress1, // Replace with actual value
                                SerialNo = snValue, // Replace with actual value
                                DeviceModel = snValue1 // Replace with actual value
                            }));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                }

            }
        }

    }
    private static readonly TimeZoneInfo TimeZoneSingapore = TimeZoneInfo.FindSystemTimeZoneById("Asia/Singapore");

    public static class TimeUtility
    {
        public static long GetTodayStartTimestamp()
        {
            DateTime today = DateTime.Now.Date;
            return new DateTimeOffset(today).ToUnixTimeSeconds();
        }

        public static long GetTodayEndTimestamp()
        {
            DateTime tomorrow = DateTime.Now.Date.AddDays(1);
            return new DateTimeOffset(tomorrow).ToUnixTimeSeconds() - 1;
        }
    }
    public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        // Unix timestamp is seconds past epoch
        DateTime epochDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime localDateTime = epochDateTime.AddSeconds(unixTimeStamp).ToUniversalTime();

        return TimeZoneInfo.ConvertTimeFromUtc(localDateTime, TimeZoneSingapore);
    }
    static string ExtractValue(string input)
    {
        return input.Split('=')[1].Trim().Replace("\\", "");
    }

    static bool ContainsRecords(Dictionary<string, object> parsedData)
    {
        return parsedData.Keys.Any(key => key.StartsWith("records[") && key.EndsWith("].AttendanceState"));
    }
    static Dictionary<string, object> ParseData(string data)
    {
        Dictionary<string, object> result = new Dictionary<string, object>();

        var lines = data.Split('\n');
        foreach (var line in lines)
        {
            var parts = line.Split('=');
            if (parts.Length == 2)
            {
                string key = parts[0].Trim();
                string value = parts[1].Trim();

                result[key] = value;
            }
        }

        return result;
    }

    static List<UnlockingRecords> ParseRecords(Dictionary<string, object> parsedData)
    {
        List<UnlockingRecords> records = new List<UnlockingRecords>();

        // Group keys by index
        var groupedKeys = parsedData.Keys
            .Where(key => key.StartsWith("records["))
            .GroupBy(key => ExtractIndexFromKey(key));

        foreach (var group in groupedKeys)
        {
            UnlockingRecords currentRecord = new UnlockingRecords();
            records.Add(currentRecord);

            foreach (var key in group)
            {
                // Extract the property name without the index and suffix
                string propName = ExtractPropertyName(key);

                // Set the property value dynamically
                SetProperty(currentRecord, propName, parsedData[key]);
            }
        }

        return records;
    }
    static int ExtractIndexFromKey(string key)
    {
        // Assuming the index is within square brackets
        int startIndex = key.IndexOf('[') + 1;
        int endIndex = key.IndexOf(']');

        if (startIndex >= 0 && endIndex >= 0 && endIndex > startIndex)
        {
            string indexString = key.Substring(startIndex, endIndex - startIndex);
            return int.Parse(indexString);
        }

        return -1; // Default index if not found
    }
    static void SetProperty(UnlockingRecords record, string propertyName, object propertyValue)
    {
        // Use reflection to set the property dynamically
        var property = typeof(UnlockingRecords).GetProperty(propertyName);

        if (property != null)
        {
            // Check if the property type is nullable
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                // If the property value is null or empty, set the property to null
                if (string.IsNullOrEmpty(propertyValue?.ToString()))
                {
                    property.SetValue(record, null);
                }
                else
                {
                    // Convert the property value to the correct nullable type
                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyType);
                    var convertedValue = Convert.ChangeType(propertyValue, underlyingType);
                    property.SetValue(record, convertedValue);
                }
            }
            else
            {
                // Convert the property value to the correct type
                var convertedValue = Convert.ChangeType(propertyValue, property.PropertyType);
                property.SetValue(record, convertedValue);
            }
        }
    }

    static string ExtractPropertyName(string key)
    {
        // Assuming the property name is between the last '.' and the '=' or the last ']'
        int startIndex = key.LastIndexOf('.') + 1;
        int endIndex = Math.Min(key.LastIndexOf('='), key.LastIndexOf(']'));

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return key.Substring(startIndex, endIndex - startIndex);
        }

        return key.Substring(startIndex); // Return the rest of the string if endIndex is -1
    }
}