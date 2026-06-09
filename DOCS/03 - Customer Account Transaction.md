# C Customer Account Transaction

### From Sage Evolution SDK | Documentation Portal

The Customer class is used to create Customer Accounts which can be used on Customer related
processes.

The following code displays how a new Customer is created with the basic necessary properties like Code
and description speciﬁed.

```csharp
//Assign variable C to Customer class
Customer C = new Customer();
//Specify Customer properties
C.Code = "CustomerSDK1";
C.Description = "supplierSDK1";
//Use the save method to Save the Customer
C.Save();
```

The same Customer can be edited and saved again with updated information like addresses and
telephone number. For Delivery Addresses it is necessary to save the Customer ﬁrst. the following
example checks if the customer exists and if the delivery code exists.

```csharp
string DC = "Del2";
string Cust = "CustomerSDK1";
//check if Customer exists note to capture a delivery address the Customer must exist first
if (Customer.FindByCode(Cust) == -1)
{
Customer NewCust = new Customer();
//NewCust.Description = Cust;
NewCust.Description = Cust;
NewCust.Save();
}
Customer C = new Customer(Cust);
//Set new properties
C.Telephone = "113456";
C.EmailAddress = "Customer@SDK";
//Set Postal or physical address
C.PostalAddress = new Address("Postal Address 1", "Post 2", "Post 3", "Post 4", "Post 5", "PC");
C.PhysicalAddress = new Address()
{
Line1 = "Physical1",
Line2 = "Physical2",
Line3 = "Physical3",
Line4 = "Physical4",
Line5 = "Physical5",
PostalCode = "2000",
};
//Check for Delivery Address code if false save
if (DeliveryAddressCode.FindByCode(DC) == -1)
{
DeliveryAddressCode delAdd = new DeliveryAddressCode();
delAdd.Code = DC;
delAdd.Description = ("Delivery address");
delAdd.Save();
}
//Specify the DeliveryAddress address for the code
Address address = new Address("102 Delivery Address", "Delivery Address 2", "2620");
C.DeliveryAddresses.Add(DC, address);
//Use the save method to Save the Customer
C.Save();
```

### Retrieved from "https://developerzone.pastel.co.za/index.php?

### title=C_Customer_Account_Transaction&oldid=127"

---

This page was last modiﬁed on 23 October 2015, at 11:04.

---

# C Customer Allocations

### From Sage Evolution SDK | Documentation Portal

Customer Transactions like Invoices can be allocated to Contra Customer Transactions like Invoice
Payments(Receipts). All Customer related transactions will appear in the PostAR table. In PostAR Accountlink
determines which Customer Account the transaction is for and the outstanding ﬁeld determines what amount
can still be allocated. A fully Allocated transaction will have a outstanding value of 0.

The callocs ﬁeld contains the allocation to the related transaction an example would be I=6;A=50;D=20150622
where I = Autoidx , A = Amount Allocated , D = Allocation Date

To Allocate two transactions you would require the Autoidx of the transaction in the PostAR for both

### transactions as follows:

```csharp
CustomerTransaction invoice = new CustomerTransaction(5);// AutoIdx of debit transaction in PostAR table
CustomerTransaction payment = new CustomerTransaction(8);// AutoIdx of credit transaction in PostAR table
invoice.Allocations.Add(payment);
invoice.Allocations.Save();
```

### Allocations can also be done at the time of posting as folows:

```csharp
//An invoice is posted.
CustomerTransaction invoice = new CustomerTransaction();
invoice.Account = new Customer("Customer1");
invoice.TransactionCode = new TransactionCode(Module.AR, "IN");
invoice.Amount = 500.50;
invoice.TaxRate = new TaxRate(7);
invoice.Reference = "INV12348902";
invoice.Description = "Customer Invoice";
invoice.Post();
//A payment(receipt) is posted.
CustomerTransaction payment = new CustomerTransaction();
payment.Account = new Customer("Customer1");
payment.TransactionCode = new TransactionCode(Module.AR, "PM");
payment.Amount = 1000.00;
payment.Reference = "EFT";
payment.Description = "Payment";
payment.Post();
//We allocate the in-memory payment object to the invoice object
invoice.Allocations.Add(payment);
//Don't forget to call save!
invoice.Allocations.Save();
```

The Following example allocates a Customer payment to a existing sales order invoice by creating and

### using the allocateorder method to get the Customer transaction id

```csharp
{
//Post a Customer payment
CustomerTransaction payment = new CustomerTransaction();
payment.Account = new Customer("Customer1");
payment.TransactionCode = new TransactionCode(Module.AR, "PM");
payment.OrderNo = "SO0018";
payment.Amount = 20;
payment.Reference = "PM123";
payment.Description = "Payment Made";
payment.Post();
//Use method allocateOrder to find order number and allocate to payment
allocateOrder(payment);
}
private void allocateOrder(DrCrTransaction transaction)
{
```

---

```csharp
string criteria = string.Format("Order_No = '{0}'", transaction.OrderNo);
if (transaction.Outstanding == 0)
return;
if (transaction.Outstanding > 0)
criteria += " and Outstanding 0";
DataTable matches = CustomerTransaction.List(transaction.Account, criteria);
if (matches.Rows.Count > 0)
{
foreach (DataRow match in matches.Rows)
{
//terminate if satisfied
if (transaction.Outstanding == 0)
break;
CustomerTransaction relatedTran = new CustomerTransaction((Int64)match["Autoidx"]);
transaction.Allocations.Add(relatedTran);
}
transaction.Allocations.Save();
}
}
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Customer_Allocations&oldid=277"

This page was last modiﬁed on 6 June 2016, at 09:48.

---

# C Customer Batches

### From Sage Evolution SDK | Documentation Portal

A Customer Batch is created in Customers | Transactions | Customers Batches.

Customer batches can take gl accounts and Supplier accounts. The batch can also be processed.

### The following is an example of a Customer Batch

```csharp
string BatchNum = "ARB1";
CustomerBatch.Get(1);
if (CustomerBatch.Find(BatchNum) == -1)
{
// Batch code does not exist, so create it
CustomerBatch createsbatch = new CustomerBatch();
createsbatch.BatchNo = BatchNum;
createsbatch.Description = "CB Batch";
createsbatch.CreatedAgent = new Agent("Admin");
createsbatch.AllowGLContraSplit = true;
createsbatch.AllowDuplicateReferences = true;
createsbatch.EnterTaxOnGlContraSplit = true;
createsbatch.Save();
MessageBox.Show(createsbatch.BatchNo);
}
//The following code populates a customer batch and processes it
CustomerBatch CB = new CustomerBatch(BatchNum);
BatchDetail BDet = new BatchDetail();
BDet.Customer = new Customer("Cash");
BDet.Date = DateTime.Now;
BDet.Description = "LineDesc";
BDet.Reference = "ref1";
BDet.PostDated = false;
BDet.TransactionCode = new TransactionCode(Module.AR, "IN");
BDet.TaxType = new TaxRate(1);
BDet.AmountExclusive = 728;
BDet.GLContraAccount = new GLAccount("accounting fees");
CB.Detail.Add(BDet);
BDet = new BatchDetail();
CB.Detail.Add(BDet);
BDet.Supplier = new Supplier("Supplier1");
BDet.Date = DateTime.Now;
BDet.Description = "LineDesc";
BDet.Reference = "ref1";
BDet.PostDated = false;
BDet.TransactionCode = new TransactionCode(Module.AP, "IN");
BDet.TaxType = new TaxRate(1);
BDet.ContraSplit.Add("Security", "Ledger Line 1", 100, "1");
BDet.ContraSplit.Add("Sales", "Ledger Line 2", 328, "1");
BDet.AmountExclusive = 428;
BDet = new BatchDetail();
BDet.GLAccount = new GLAccount("Advertising");
BDet.Date = DateTime.Now;
BDet.Description = "LineDesc";
BDet.Reference = "ref1";
BDet.PostDated = false;
BDet.IsDebit = true;
BDet.TransactionCode = new TransactionCode(Module.GL, "JNL");
BDet.TaxType = new TaxRate(1);
BDet.ContraSplit.Add("Security", "Ledger Line 1", 100, "1");
BDet.ContraSplit.Add("Sales", "Ledger Line 2", 428, "1");
BDet.ContraSplit.Add("Accounting fees", "Ledger Line 3", 500, "3");
BDet.AmountExclusive = 1028;
CB.Detail.Add(BDet);
CB.Process();
```

---

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Customer_Batches&oldid=320"

This page was last modiﬁed on 18 July 2017, at 16:23.

---

# C Customer Transaction

### From Sage Evolution SDK | Documentation Portal

A Customer Transaction is the Equivalent of a Standard transaction done in Evolution | Accounts Receivable |
Transactions | Standard. Customer transactions affect the General Ledger and the Debtors Ledger and are
typically used to post payments(receipts) for customers since invoices done here do not affect Inventory or store
as a source document.

The only required values are Account, Reference and TransactionCode. Notice how the Account property is set
to an instance of the Customer class. The transaction code deﬁnes whether the transaction is a debit or credit
(hence the Amount is always positive) and also determines which accounts to post to in the general ledger.
There are a number of default transaction codes conﬁgured in a new Evolution database (such as IN and CN),
but the list is often extended, so it would be wise to make transaction codes conﬁgurable in your application.
Finally, the Post method is called. This processes the transaction to the account immediately and adjusts the
account balance accordingly. Other interesting ﬁelds are the transaction date (defaults to current date),
description (defaults to transaction code description) and order number (empty by default).

The following is a example of a Customer Transaction using the IN Transaction Type

```csharp
// Declare Customer Transaction Class
CustomerTransaction CustTran = new CustomerTransaction();
//Instance of Customer class
CustTran.Customer = new Customer("Customer1");
CustTran.TransactionCode = new TransactionCode(Module.AR, "IN");
CustTran.TaxRate = new TaxRate("1");
CustTran.Amount = 100;
CustTran.Reference = "AP_Ref1";
CustTran.Description = "StringDescription";
//Post Method to Commit Transaction
CustTran.Post();
```

The following example posts Customer transactions as a batch using the same Audit number

```csharp
{
ConsolidateCustomers();
}
private static GLBatch _batch = new GLBatch();
public static void ConsolidateCustomers()//create method consolidatesuppliers
{
try
{
DatabaseContext.BeginTran();
_batch.Clear();
//Transaction 1
CustomerTransaction custtran = new CustomerTransaction();
custtran.GLDebitPosting += new TransactionBase.GLPostingEventHandler(custtran_GLDebitPosting);
custtran.GLCreditPosting += new TransactionBase.GLPostingEventHandler(custtran_GLCreditPosting);
custtran.Account = new Customer("Customer1");
custtran.Amount = 50;
custtran.TaxRate = new TaxRate(7);
custtran.Reference = custtran.Description = "inv1";
custtran.TransactionCode = new TransactionCode(Module.AR, "IN");
custtran.Post();
//Transaction 2
custtran = new CustomerTransaction();
custtran.GLDebitPosting += new TransactionBase.GLPostingEventHandler(custtran_GLDebitPosting);
custtran.GLCreditPosting += new TransactionBase.GLPostingEventHandler(custtran_GLCreditPosting);
custtran.Account = new Customer("Customer1");
custtran.Amount = 60;
custtran.TaxRate = new TaxRate(7);
custtran.Reference = custtran.Description = "inv2";
custtran.TransactionCode = new TransactionCode(Module.AR, "IN");
custtran.Post();
//Transaction 3
```

---

```csharp
custtran = new CustomerTransaction();
custtran.GLDebitPosting += new TransactionBase.GLPostingEventHandler(custtran_GLDebitPosting);
custtran.GLCreditPosting += new TransactionBase.GLPostingEventHandler(custtran_GLCreditPosting);
custtran.Account = new Supplier("Customer1");
custtran.Amount = 70;
custtran.TaxRate = new TaxRate(7);
custtran.Reference = custtran.Description = "inv3";
custtran.TransactionCode = new TransactionCode(Module.AR, "IN");
custtran.Post();
_batch.Post();
DatabaseContext.CommitTran();
}
catch (Exception ex)
{
DatabaseContext.RollbackTran();
MessageBox.Show(ex.Message);
}
}
static void custtran_GLCreditPosting(TransactionBase sender, TransactionBase.GLPostingEventArgs e)
{
_batch.Add((GLTransaction)e.GLTransaction.Clone());
e.Posted = true;
}
static void custtran_GLDebitPosting(TransactionBase sender, TransactionBase.GLPostingEventArgs e)
{
_batch.Add((GLTransaction)e.GLTransaction.Clone(), true, true);
e.Posted = true;
}
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Customer_Transaction&oldid=275"

This page was last modiﬁed on 6 June 2016, at 09:32.
