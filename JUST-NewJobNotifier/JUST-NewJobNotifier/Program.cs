using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Odbc;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using JUST.NewJobNotifier.Classes;

namespace JUST.NewJobNotifier
{
    class MainClass
    {
        /* version 1.00a  */
        private const string debug = "debug";
        private const string live = "live";
        private const string monitor = "monitor";

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private static string Uid;
        private static string Pwd;
        private static string FromEmailAddress;
        private static string FromEmailPassword;
        private static string FromEmailSMTP;
        private static int? FromEmailPort;
        private static string Mode;
        private static string[] MonitorEmailAddresses;
        private static ArrayList ValidModes = new ArrayList() { debug, live, monitor };

        private static string EmailSubject = "New Jobs created";
        private static string MessageBodyFormat = @"
    <body style = ""margin-left: 20px; margin-right:20px"" >
        <hr/>
        <h2> New Jobs Created</h2>
        <hr/>

        <table style = ""width:80%; text-align: left"" border=""1"" cellpadding=""10"" cellspacing=""0"">
            <tr style = ""background-color: cyan"" >
                <th>Customer</th>
                <th>Customer Name</th>
                <th>Job Number</th>
                <th>Job Description</th>
            </tr>";

        private static string messageBodyTableItem = @"<tr>
                <td>{0}</td>
                <td>{1}</td>
                <td>{2}</td>
                <td>{3}</td>
            </tr>";
        private static string messageBodyTail = @"</table></body>";

        static void Main(string[] args)
        {
            try
            {
                log.Info("[Main] Starting up at " + DateTime.Now);

                getConfiguration();

                ProcessNewJobData();

                log.Info("[Main] Completion at " + DateTime.Now);
            }
            catch (Exception ex)
            {
                log.Error("[Main] Error: " + ex.Message);
            }
        }

        private static void getConfiguration()
        {
            Uid = ConfigurationManager.AppSettings["Uid"];
            Pwd = ConfigurationManager.AppSettings["Pwd"];
            FromEmailAddress = ConfigurationManager.AppSettings["FromEmailAddress"];
            FromEmailPassword = ConfigurationManager.AppSettings["FromEmailPassword"];
            FromEmailSMTP = ConfigurationManager.AppSettings["FromEmailSMTP"];
            FromEmailPort = Convert.ToInt16(ConfigurationManager.AppSettings["FromEmailPort"]);

            Mode = ConfigurationManager.AppSettings["Mode"].ToLower();
            var ExecutiveEmailAddressList = ConfigurationManager.AppSettings["ExecutiveEmailAddresses"];
            if (ExecutiveEmailAddressList.Length > 0)
            {
                char[] delimiterChars = { ';', ',' };
                MonitorEmailAddresses = ExecutiveEmailAddressList.Split(delimiterChars);
            }

            #region Validate Configuration Data
            var errorMessage = new StringBuilder();
            if (String.IsNullOrEmpty(Uid))
            {
                errorMessage.Append("User ID (Uid) is Required");
            }

            if (String.IsNullOrEmpty(Pwd))
            {
                errorMessage.Append("Password (Pwd) is Required");
            }

            if (String.IsNullOrEmpty(FromEmailAddress))
            {
                errorMessage.Append("From Email Address (FromEmailAddress) is Required");
            }

            if (String.IsNullOrEmpty(FromEmailPassword))
            {
                errorMessage.Append("From Email Password (FromEmailPassword) is Required");
            }

            if (String.IsNullOrEmpty(FromEmailSMTP))
            {
                errorMessage.Append("From Email SMTP (FromEmailSMTP) address is Required");
            }

            if (!FromEmailPort.HasValue)
            {
                errorMessage.Append("From Email Port (FromEmailPort) is Required");
            }

            if (String.IsNullOrEmpty(Mode))
            {
                errorMessage.Append("Mode is Required");
            }

            if (!ValidModes.Contains(Mode.ToLower()))
            {
                errorMessage.Append(String.Format("{0} is not a valid Mode.  Valid modes are 'debug', 'live' and 'monitor'", Mode));
            }

            if ((Mode == monitor) || (Mode == debug))
            {
                log.Info("checking Executive Email Address List");
                if (MonitorEmailAddresses == null || MonitorEmailAddresses.Length == 0)
                {
                    errorMessage.Append("Executive Email Address is Required in monitor mode");
                }
                log.Info("finished checking ExecutiveEmailAddresses");
            }

            if (errorMessage.Length > 0)
            {
                throw new Exception(errorMessage.ToString());
            }
            #endregion
        }

        private static void ProcessNewJobData()
        {
            try
            {
                OdbcConnection cn;
                OdbcCommand cmd;
                var notifiedlist = new ArrayList();

                //jcjob
                // user_1 = job description
                // user_2 = sales person
                // user_3 = designer
                // user_4 = project manager
                // user_5 = SM
                // user_6 = Fitter
                // user_7 = Plumber
                // user_8 = Tech 1
                // user_9 = Tech 2
                // user_10 = Notified
                //
                //customer
                // user_1 = primary contact
                // user_2 = secondary contact
//                var NewJobsQuery = "Select jcjob.cusnum, jcjob.jobnum, jcjob.name as jobName, customer.name as customerName, jcjob.user_10 as notified, customer.user_1 as primaryContact, customer.user_2 as secondaryContact from jcjob inner join customer on jcjob.cusnum = customer.cusnum where jcjob.jobnum = '12-3031' order by customer.user_1 asc";
                var NewJobsQuery = "Select jcjob.cusnum, jcjob.jobnum, jcjob.name as jobName, customer.name as customerName, jcjob.user_10 as notified, customer.user_1 as primaryContact, customer.user_2 as secondaryContact from jcjob inner join customer on jcjob.cusnum = customer.cusnum where jcjob.user_10 = 0 order by customer.user_1 asc";

                OdbcConnectionStringBuilder just = new OdbcConnectionStringBuilder();
                just.Driver = "ComputerEase";
                just.Add("Dsn", "Company 0");
                just.Add("Uid", Uid);
                just.Add("Pwd", Pwd);

                cn = new OdbcConnection(just.ConnectionString);
                cmd = new OdbcCommand(NewJobsQuery, cn);
                cn.Open();
                log.Info("[ProcessNewJobsData] Connection to database opened successfully");

                OdbcDataReader reader = cmd.ExecuteReader();
                try
                {

                    var EmployeeEmailAddresses = GetEmployees(cn);
                    var customerNumberColumn = reader.GetOrdinal("cusnum");
                    var jobNumColumn = reader.GetOrdinal("jobnum");
                    var jobNameColumn = reader.GetOrdinal("JobName");
                    var customerNameColumn = reader.GetOrdinal("customerName");
                    var primaryContactColumn = reader.GetOrdinal("primaryContact");
                    var secondaryContactColumn = reader.GetOrdinal("secondaryContact");
                    var notifiedColumn = reader.GetOrdinal("notified");
                    var executiveNewJobNotifications = new List<JobInformation>();

                    while (reader.Read())
                    {
                        var customerNumber = reader.GetString(customerNumberColumn);
                        var customerName = reader.GetString(customerNameColumn);
                        var jobNumber = reader.GetString(jobNumColumn);
                        var jobName = reader.GetString(jobNameColumn);
                        var primaryContact = reader.GetString(primaryContactColumn).ToLower();
                        var secondaryContact = reader.GetString(secondaryContactColumn).ToLower();
                        var primaryContactEmployee = GetEmployeeInformation(EmployeeEmailAddresses, primaryContact);
                        var secondaryContactEmployee = secondaryContact.Length > 0 ? GetEmployeeInformation(EmployeeEmailAddresses, secondaryContact) : new Employee();

                        log.Info("[ProcessNewJobsData] ----------------- Found New Job Number " + jobNumber + " -------------------");
/*                        log.Info("customerNumber: " + customerNumber);
                        log.Info("jobNumber: " + jobNumber);
                        log.Info("jobName: " + jobName);
                        log.Info("primaryContact: " + primaryContact + ", " + primaryContactEmployee.EmailAddress);
                        log.Info("secondaryContact: " + secondaryContact);
                        log.Info("customerName: " + customerName);
                        log.Info("notified: " + reader.GetInt16(notifiedColumn).ToString());*/

                        primaryContactEmployee.AddJobToNotify(jobNumber, jobName, customerNumber, customerName);

                        log.Info(" new job " + jobNumber + " added to " + primaryContactEmployee.EmailAddress);
                        if (secondaryContactEmployee.EmailAddress.Length > 0)
                        {
                            secondaryContactEmployee.AddJobToNotify(jobNumber, jobName, customerNumber, customerName);
                        }

                        if ((Mode == monitor) || (Mode == debug))
                        {
                            executiveNewJobNotifications.Add(new JobInformation() {JobNumber = jobNumber, JobName = jobName, CustomerNumber = customerNumber, CustomerName = customerName});
                        }
                    }

                    log.Info(" Live/Debug Email Block ");
                    log.Info(" Mode: " + Mode.ToString());
                    if ((Mode == live) || (Mode == monitor))
                    {
                        log.Info(" Mode: " + Mode.ToString());
                        foreach (var emp in EmployeeEmailAddresses)
                        {
                            log.Info(" emp: " + emp.Name + ", " + emp.EmailAddress);
                            log.Info(" emp.NewJobs: " + emp.NewJobs.Count());
                            if (emp.NewJobs.Count > 0)
                            {
                                var message = MessageBodyFormat;

                                log.Info("Notify " + emp.Name + " of new jobs: ");
                                foreach (var job in emp.NewJobs)
                                {
                                    message += string.Format(messageBodyTableItem, job.CustomerNumber, job.CustomerName, job.JobNumber, job.JobName);
                                }

                                message += messageBodyTail;

                                if (sendEmail(emp.EmailAddress, EmailSubject, message))
                                {
                                    foreach (var job in emp.NewJobs)
                                    {
                                        if (!notifiedlist.Contains(job.JobNumber))
                                        {
                                            log.Info(" job '" + job.JobNumber + "' not found in notifiedList");
                                            notifiedlist.Add(job.JobNumber);
                                        }
                                    }
                                }
                                log.Info(message);
                            }
                        }
                    }

                    log.Info(" Executive Email Block ");
                    log.Info(" ExecutiveNewJobNotifications: " + executiveNewJobNotifications.Count());
                    if (((Mode == monitor) || (Mode == debug)) && (executiveNewJobNotifications.Count() > 0))
                    {
                        var excutiveMessage = MessageBodyFormat;
                        foreach (var job in executiveNewJobNotifications)
                        {
                            excutiveMessage += string.Format(messageBodyTableItem, job.CustomerNumber, job.CustomerName, job.JobNumber, job.JobName);
                        }

                        excutiveMessage += messageBodyTail;

                        foreach(var executive in MonitorEmailAddresses)
                        {
                            if (sendEmail(executive, EmailSubject, excutiveMessage))
                            {
                                foreach (var job in executiveNewJobNotifications)
                                {
                                    if (!notifiedlist.Contains(job.JobNumber))
                                    {
                                        notifiedlist.Add(job.JobNumber);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception x)
                {
                    log.Error("[ProcessNewJobsData] Reader Error: " + x.Message);
                }

                foreach (var jobNum in notifiedlist)
                {
                    log.Info("update job " + jobNum + " as notified");
                    try
                    {
                        var updateCommand = string.Format("update jcjob set \"user_10\" = 1 where jcjob.jobnum = '{0}'", jobNum);
                        cmd = new OdbcCommand(updateCommand, cn);
                        cmd.ExecuteNonQuery();
                    }
                    catch (Exception x)
                    {
                        log.Error(String.Format("[ProcessNewJobsData] Error updating Job Number {0} to be Notified: {1}", jobNum, x.Message));
                    }
                }

                reader.Close();
                cn.Close();
            }
            catch (Exception x)
            {
                log.Error("[ProcessNewJobsData] Exception: " + x.Message);
                return;
            }

            return;
        }

        private static Employee GetEmployeeInformation(List<Employee> EmployeeEmailAddresses, string employee)
        {
            try
            {
                var e = EmployeeEmailAddresses.FirstOrDefault(x => x.EmployeeId.ToLowerInvariant() == employee.ToLowerInvariant());

                if (e != null && e.EmailAddress.Length > 0)
                {
                    return e;
                }
            }
            catch (KeyNotFoundException)
            {
                log.Info("[GetEmployeeInformation] No Employee record found by employeeid for : " + employee);
            }
            catch (Exception x)
            {
                log.Error("[GetEmployeeInformation] by employeeid exception: " + x.Message);
            }

            try
            {
                var e = EmployeeEmailAddresses.FirstOrDefault(x => x.Name.ToLowerInvariant() == employee.ToLowerInvariant());

                if (e != null && e.EmailAddress.Length > 0)
                {
                    return e;
                }
            }
            catch (KeyNotFoundException)
            {
                log.Info("[GetEmployeeInformation] No Employee record found by name for : " + employee);
            }
            catch (Exception x)
            {
                log.Error("[GetEmployeeInformation] by name exception: " + x.Message);
            }

            return new Employee();
        }

        private static string FormatEmailBody(string receivedOnDate, string purchaseOrderNumber, string receivedBy, string bin, string buyerName, string vendor, JobInformation job, string notes)
        {
            var purchaseOrderItemTable = string.Empty;
            foreach (PurchaseOrderItem poItem in job.PurchaseOrderItems)
            {
                purchaseOrderItemTable += string.Format(messageBodyTableItem, poItem.ItemNumber, poItem.Description, poItem.Quantity);
            }

            var emailBody = String.Format(MessageBodyFormat, purchaseOrderNumber, receivedBy, receivedOnDate, bin, job.JobNumber, job.JobName, job.CustomerName, vendor, buyerName, notes) + purchaseOrderItemTable + messageBodyTail;

            return emailBody;
        }
        /*
        private static void NotifyEmployee(ArrayList notifiedlist, string poNum, string employeeEmailAddress, string receivedBy, string bin, string emailSubject, string emailBody)
        {
            try
            {
                if (employeeEmailAddress.Length > 0)
                {
                    log.Info("  [NotifyEmployee]   sending email to: " + employeeEmailAddress);
                    if (sendEmail(employeeEmailAddress, emailSubject, emailBody))
                    {
                        notifiedlist.Add(poNum);
                    }
                }
                else
                {
                    log.Error("  [NotifyEmployee]  Purchase Order does not have an email address defined [" + emailSubject + "]");
                }
            }
            catch (Exception ex)
            {
                log.Info("  [NotifyEmployee] Error " + ex.Message);
            }
        }
        */
        private static bool sendEmail(string toEmailAddress, string subject, string emailBody)
        {
            bool result = true;
            if (toEmailAddress.Length == 0)
            {
                log.Error("  [sendEmail] No toEmailAddress to send message to");
                return false;
            }

            log.Info("  [sendEmail] Sending Email to: " + toEmailAddress);
            log.Info("  [sendEmail] EmailMessage: " + emailBody);

            try
            {
                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(FromEmailAddress, "New Job Notification");
                    mail.To.Add(toEmailAddress);
                    mail.Subject = subject;
                    mail.Body = emailBody;
                    mail.IsBodyHtml = true;

                    using (SmtpClient smtp = new SmtpClient(FromEmailSMTP, FromEmailPort.Value))
                    {
                        smtp.Credentials = new NetworkCredential(FromEmailAddress, FromEmailPassword);
                        smtp.EnableSsl = true;
                        smtp.Send(mail);
                        log.Info("  [sendEmail] Email Sent to " + toEmailAddress);
                    }
                }
            }
            catch (Exception x)
            {
                result = false;
                log.Error(String.Format("  [sendEmail] Error Sending email to {0}, message: {1}", x.Message, emailBody));
            }

            return result;
        }

        private static List<Employee> GetEmployees(OdbcConnection cn)
        {
            var employees = new List<Employee>();

            var buyerQuery = "Select user_1, user_2, name from premployee where user_1 is not null";
            var buyerCmd = new OdbcCommand(buyerQuery, cn);

            OdbcDataReader buyerReader = buyerCmd.ExecuteReader();

            while (buyerReader.Read())
            {
                var buyer = buyerReader.GetString(0);
                var email = buyerReader.GetString(1);
                var name = buyerReader.GetString(2);

                if (buyer.Trim().Length > 0)
                {
                    employees.Add(new Employee(buyer, name, email));
                }
            }

            buyerReader.Close();

            return employees;
        }
    }
}
/*
log.Info("column names");
for (int col = 0; col < reader.FieldCount; col++)
{
    log.Info(reader.GetName(col));
}
*/
