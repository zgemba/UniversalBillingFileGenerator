using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;

namespace UniversalBillingFileGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            int months = int.Parse(ConfigurationManager.AppSettings["Months"]);
            string frmt = ConfigurationManager.AppSettings["Format"];
            string table = ConfigurationManager.AppSettings["TableName"];
            string filename = "billing";

            DateTime now = DateTime.UtcNow;
            DateTime start = now.AddMonths(-months);

            string query = string.Format("SELECT * FROM {0} WHERE TransmissionTime BETWEEN '{1}' AND '{2}' ORDER BY BillableItemId",
                                         table, 
                                         start.ToString(CultureInfo.InvariantCulture), 
                                         now.ToString(CultureInfo.InvariantCulture));

            using (StreamWriter sw = new StreamWriter(filename + ".psv"))
            {
                string cs = ConfigurationManager.ConnectionStrings["BillingData"].ConnectionString;
                using (SqlConnection conn = new SqlConnection(cs))
                {
                    SqlCommand cmd = new SqlCommand(query, conn);
                    conn.Open();

                    SqlDataReader rdr = cmd.ExecuteReader();
                    while (rdr.Read())
                    {
                        switch (frmt)
                        {
                            case "EMA":
                                sw.Write(rdr["TransmissionTime"]);
                                sw.Write("|");
                                int size = (int)rdr["Size"];
                                if ((string)rdr["Channel"] == "Iridium")
                                {
                                    // Iridium plan B obračuna najmanj 30! Če bomo uporabljali plan A je to 10, 
                                    //če mešano je to zaokroževanje potrebno premakniti v billing apliakcijo 
                                    size = (size < 30) ? 30 : size;
                                }
                                sw.Write(size);
                                sw.Write('|');
                                sw.Write(rdr["Channel"]);
                                sw.Write('|');
                                sw.Write(rdr["PublicDeviceID"]);
                                sw.Write('|');
                                sw.Write(rdr["Direction"]);
                                sw.Write('|');
                                sw.Write(rdr["Type"]);
                                sw.WriteLine();
                                break;

                            case "AST":
                                sw.Write(rdr["TransmissionTime"]);
                                sw.Write('|');
                                sw.Write(rdr["Size"]);
                                sw.Write('|');
                                sw.Write(rdr["Channel"]);
                                sw.Write('|');
                                if (rdr["DeviceDestinationID"] != null) sw.Write((int)rdr["DeviceDestinationID"]);
                                sw.Write('|');
                                if (rdr["GeozoneAddressID"] != null) sw.Write((long)rdr["GeozoneAddressID"]);
                                sw.Write('|');
                                sw.Write(rdr["IMEI"]);
                                sw.Write('|');
                                sw.Write(rdr["MessageName"]);
                                sw.Write('|');
                                sw.Write(rdr["AuthorityName"]);
                                sw.Write('|');
                                if ((string)rdr["MessageName"] == "ascii") sw.Write(false);
                                else if (rdr["IsPollResponse"] != null) sw.Write(rdr["IsPollResponse"]);
                                sw.Write('|');
                                if (rdr["GpsTime"] != null) sw.Write(rdr["GpsTime"]);
                                sw.WriteLine();
                                break;
                            default: break;
                        }
                    }
                }
            }

            if (frmt == "EMA")          // potrebujem še devices.csv
            {
                using (StreamWriter sw = new StreamWriter("devices.csv"))
                {
                    string cs = ConfigurationManager.ConnectionStrings["TDS"].ConnectionString;
                    using (SqlConnection conn = new SqlConnection(cs))
                    {
                        query = @"SELECT d.deviceSN, d.ChannelIdent1, a.Username
                                FROM Device d, Account a
                                WHERE a.ID = d.AccountID
                                AND (d.ChannelIdent1 IS NOT NULL AND d.ChannelIdent1 <> '')
                                ORDER BY d.DeviceSN";

                        SqlCommand cmd = new SqlCommand(query, conn);
                        conn.Open();
                        SqlDataReader rdr = cmd.ExecuteReader();

                        while (rdr.Read())
                        {
                            sw.WriteLine(String.Format("{0},{1},{2}",
                                        int.Parse((string)rdr[0]), rdr[1], rdr[2]));
                        }
                    }
                }
            }

            // zip
            if (File.Exists(filename+".zip"))
                File.Delete(filename + ".zip");

            using (ZipArchive zip = ZipFile.Open(filename + ".zip", ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(filename + ".psv", filename + ".psv", CompressionLevel.Optimal);
                if (frmt == "EMA")
                    zip.CreateEntryFromFile("devices.csv", "devices.csv", CompressionLevel.Optimal);
            }

            // mail
            MailMessage mail = new MailMessage();
            mail.Subject = "TDS billing file from " + start.Date + " to " + now.Date + " @ " + Environment.MachineName;
            mail.From = new MailAddress(ConfigurationManager.AppSettings["FromEmail"]);
            string toEmail = ConfigurationManager.AppSettings["ToEmail"];
            foreach (string rec in toEmail.Split(new char[] { ',', ';', ':' }))
                mail.To.Add(rec);
            SmtpClient client = new SmtpClient(
                ConfigurationManager.AppSettings["SmtpServer"],
                int.Parse(ConfigurationManager.AppSettings["SmtpPort"]));
            client.Credentials = new NetworkCredential(
                ConfigurationManager.AppSettings["SmtpUsername"],
                ConfigurationManager.AppSettings["SmtpPassword"]);
            FileStream stream = new FileStream(filename + ".zip", FileMode.Open);
            Attachment attach = new Attachment(stream, filename + ".zip");
            mail.Attachments.Add(attach);
            client.Send(mail);              
        }
    }
}
