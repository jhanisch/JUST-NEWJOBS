using System;
using System.Collections;
using System.Collections.Generic;

namespace JUST.NewJobNotifier.Classes
{
    public enum TypeOfWork
    {
        Job,
        WorkOrder 
    }

    public class QueryType
    {
        public QueryType()
        {
            Query = string.Empty;
        }

        public QueryType(string query, TypeOfWork workType)
        {
            Query = query;
            WorkType = workType;
        }

        public string Query;
        public TypeOfWork WorkType;
    }

    public class Employee
    {
        public Employee()
        {
            EmployeeId = string.Empty;
            Name = string.Empty;
            EmailAddress = string.Empty;
            NewJobs = new List<JobInformation>();
        }

        public Employee(string employeeId, string name, string emailAddress)
        {
            EmployeeId = employeeId;
            Name = name;
            EmailAddress = emailAddress;
            NewJobs = new List<JobInformation>();
        }

        public void AddJobToNotify(TypeOfWork typeOfWork, string job, string jobDescription, string cusNum, string customerName, string siteNumber)
        {
            NewJobs.Add(new JobInformation() {WorkType = typeOfWork, JobNumber = job, JobName = jobDescription, CustomerNumber = cusNum, CustomerName = customerName, SiteNumber = siteNumber});
        }

        public string EmployeeId { get; set; }
        public string Name { get; set; }
        public string EmailAddress { get; set; } 
        public List<JobInformation> NewJobs { get; }
    }

    public class PurchaseOrderItem
    {
        public PurchaseOrderItem()
        {
            ItemNumber = string.Empty;
            Description = string.Empty;
            Quantity = string.Empty;
            UnitPrice = string.Empty;
        }

        public PurchaseOrderItem(string itemNumber, string description, string quantity = "", string unitPrice = "")
        {
            ItemNumber = itemNumber;
            Description = description;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }

        public string ItemNumber { get; set; }
        public string Description { get; set; }
        public string Quantity { get; set; }
        public string UnitPrice { get; set; }
        public long Received { get; set; }
    }

    public class JobInformation
    {
        public JobInformation()
        {
            ProjectManagerName = string.Empty;
            JobNumber = string.Empty;
            JobName = string.Empty;
            CustomerNumber = string.Empty;
            CustomerName = string.Empty;
            SiteNumber = string.Empty;
            PurchaseOrderItems = new List<PurchaseOrderItem>();
        }

        public JobInformation(TypeOfWork workType, string projectManagerName, string jobNumber, string jobName, string customerNumber, string siteNumber)
        {
            WorkType = workType;
            ProjectManagerName = projectManagerName;
            JobNumber = jobNumber;
            JobName = jobName;
            CustomerNumber = customerNumber;
            SiteNumber = siteNumber;
            PurchaseOrderItems = new List<PurchaseOrderItem>();
        }

        public TypeOfWork WorkType { get; set; }
        public string ProjectManagerName { get; set; }
        public string JobNumber { get; set; }
        public string JobName { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string SiteNumber { get; set; }
        public IList<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    }
}
