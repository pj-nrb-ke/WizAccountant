# CSupplierAccountTransaction

### From Sage Evolution SDK | Documentation Portal

The supplier class is used to create Supplier Accounts which can be used on supplier related processes.

The following code displays how a new supplier is created with the basic necessary properties like Code and
description speciﬁed.

```csharp
//Assign variable S to Supplier class
Supplier S = new Supplier();
//Specify Supplier properties
S.Code = "SupplierSDK1";
S.Description = "supplierSDK1";
//Use the save method to Save the Supplier
S.Save();
```

The same supplier can be edited and saved again with updated information like addresses and telephone
number.

```csharp
Supplier S = new Supplier("SupplierSDK1");
//Set new properties
S.Telephone = "113456";
S.EmailAddress = "Supplier@SDK";
//Set Postal or physical address
S.PostalAddress = new Address("Postal Address 1", "Post 2", "Post 3", "Post 4", "Post 5", "PC");
S.PhysicalAddress = new Address()
{
Line1 = "Physical1",
Line2 = "Physical2",
Line3 = "Physical3",
Line4 = "Physical4",
Line5 = "Physical5",
PostalCode = "2000",
};
//Use the save method to Save the Supplier
S.Save();
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=CSupplierAccountTransaction&oldid=163"

This page was last modiﬁed on 26 October 2015, at 09:29.

---

# CSupplierAllocations

### From Sage Evolution SDK | Documentation Portal

Supplier Transactions like Invoices can be allocated to Contra Supplier Transactions like Supplier Payments.
All Supplier related transactions will appear in the PostAP table. In PostAP Accountlink determines which
Supplier Account the transaction is for and the outstanding ﬁeld determines what amount can still be allocated.
A fully Allocated transaction will have a outstanding value of 0.

The callocs ﬁeld contains the allocation to the related transaction an example would be I=6;A=50;D=20150622
where I = Autoidx , A = Amount Allocated , D = Allocation Date

To Allocate two transactions you would require the Autoidx of the transaction in the PostAP for both

### transactions as follows:

```csharp
SupplierTransaction invoice = new SupplierTransaction(5);// AutoIdx of debit transaction in PostAP table
SupplierTransaction payment = new SupplierTransaction(8);// AutoIdx of credit transaction in PostAP table
invoice.Allocations.Add(payment);
invoice.Allocations.Save();
```

### Allocations can also be done at the time of posting as folows:

```csharp
//An invoice is posted.
SupplierTransaction invoice = new SupplierTransaction();
invoice.Account = new Supplier("Supplier1");
invoice.TransactionCode = new TransactionCode(Module.AP, "IN");
invoice.Amount = 500.50;
invoice.TaxRate = new TaxRate(7);
invoice.Reference = "SINV12348902";
invoice.Description = "Supplier Invoice";
invoice.Post();
//A payment is posted.
SupplierTransaction payment = new SupplierTransaction();
payment.Account = new Supplier("Supplier1");
payment.TransactionCode = new TransactionCode(Module.AP, "PM");
payment.Amount = 1000.00;
payment.Reference = "EFT";
payment.Description = "Payment";
payment.Post();
//We allocate the in-memory payment object to the invoice object
invoice.Allocations.Add(payment);
//Don't forget to call save!
invoice.Allocations.Save();
```

The Following example allocates a supplier payment to a existing order invoice by creating and using the

### allocateorder method to get the supplier transaction id

```csharp
{
//Post a supplier payment
SupplierTransaction payment = new SupplierTransaction();
payment.Account = new Supplier("Supplier1");
payment.TransactionCode = new TransactionCode(Module.AP, "PM");
payment.OrderNo = "PO0018";
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
criteria += " and Outstanding < 0";
else
criteria += " and Outstanding > 0";
DataTable matches = SupplierTransaction.List(transaction.Account, criteria);
if (matches.Rows.Count > 0)
{
foreach (DataRow match in matches.Rows)
{
//terminate if satisfied
if (transaction.Outstanding == 0)
break;
SupplierTransaction relatedTran = new SupplierTransaction((Int64)match["Autoidx"]);
transaction.Allocations.Add(relatedTran);
}
transaction.Allocations.Save();
}
}
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=CSupplierAllocations&oldid=278"

This page was last modiﬁed on 6 June 2016, at 09:50.

---

# CSupplierBatches

### From Sage Evolution SDK | Documentation Portal

A Supplier Batch is created in Suppliers | Transactions | Suppliers Batches.

Supplier batches can take gl accounts and Customer accounts. The batch can also be processed.

### The following is an example of a Supplier Batch

```csharp
string SBatchNum = "APB1";
SupplierBatch.Get(1);
if (SupplierBatch.Find(SBatchNum) == -1)
{
// Batch code does not exist, so create it
SupplierBatch createsbatch = new SupplierBatch();
createsbatch.BatchNo = SBatchNum;
createsbatch.Description = "SB Batch";
createsbatch.CreatedAgent = new Agent("Admin");
createsbatch.AllowGLContraSplit = true;
createsbatch.AllowDuplicateReferences = true;
createsbatch.EnterTaxOnGlContraSplit = true;
createsbatch.Save();
MessageBox.Show(createsbatch.BatchNo);
}
//The following code populates a Supplierbatch and processes it
SupplierBatch SB = new SupplierBatch(SBatchNum);
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
SB.Detail.Add(BDet);
BDet = new BatchDetail();
SB.Detail.Add(BDet);
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
SB.Detail.Add(BDet);
SB.Process();
```

---

Retrieved from "https://developerzone.pastel.co.za/index.php?title=CSupplierBatches&oldid=324"

This page was last modiﬁed on 18 July 2017, at 16:33.

---

# CSupplierTransaction

### From Sage Evolution SDK | Documentation Portal

A Supplier Transaction is the Equivalent of a Standard transaction done in Evolution | Accounts Payable |
Transactions | Standard. Supplier transactions affect the General Ledger and the Creditors Ledger and are
typically used to post payments to suppliers since invoices done here do not affect Inventory or store as a source
document.

The only required values are Account, Reference and TransactionCode. Notice how the Account property is set
to an instance of the Customer class. The transaction code deﬁnes whether the transaction is a debit or credit
(hence the Amount is always positive) and also determines which accounts to post to in the general ledger.
There are a number of default transaction codes conﬁgured in a new Evolution database (such as IN and CN),
but the list is often extended, so it would be wise to make transaction codes conﬁgurable in your application.
Finally, the Post method is called. This processes the transaction to the account immediately and adjusts the
account balance accordingly. Other interesting ﬁelds are the transaction date (defaults to current date),
description (defaults to transaction code description) and order number (empty by default).

The following is a example of a Supplier Transaction using the IN Transaction Type

```csharp
// Declare Supplier Transaction Class
SupplierTransaction SuppTran = new SupplierTransaction();
//Instance of Supplier class
SuppTran.Supplier = new Supplier("Supplier1");
SuppTran.TransactionCode = new TransactionCode(Module.AP, "IN");
SuppTran.TaxRate = new TaxRate("1");
SuppTran.Amount = 100;
SuppTran.Reference = "AP_Ref1";
SuppTran.Description = "StringDescription";
//Post Method to Commit Transaction
SuppTran.Post();
```

The following example posts supplier transactions as a batch using the same Audit number

```csharp
{
ConsolidateSuppliers();
}
private static GLBatch _batch = new GLBatch();
public static void ConsolidateSuppliers()//create method consolidatesuppliers
{
try
{
DatabaseContext.BeginTran();
_batch.Clear();
//Transaction 1
SupplierTransaction supptran = new SupplierTransaction();
supptran.GLDebitPosting += new TransactionBase.GLPostingEventHandler(supptran_GLDebitPosting);
supptran.GLCreditPosting += new TransactionBase.GLPostingEventHandler(supptran_GLCreditPosting);
supptran.Account = new Supplier("Supplier1");
supptran.Amount = 50;
supptran.TaxRate = new TaxRate(7);
supptran.Reference = supptran.Description = "inv1";
supptran.TransactionCode = new TransactionCode(Module.AP, "IN");
supptran.Post();
//Transaction 2
supptran = new SupplierTransaction();
supptran.GLDebitPosting += new TransactionBase.GLPostingEventHandler(supptran_GLDebitPosting);
supptran.GLCreditPosting += new TransactionBase.GLPostingEventHandler(supptran_GLCreditPosting);
supptran.Account = new Supplier("Supplier1");
supptran.Amount = 60;
supptran.TaxRate = new TaxRate(7);
supptran.Reference = supptran.Description = "inv2";
supptran.TransactionCode = new TransactionCode(Module.AP, "IN");
supptran.Post();
//Transaction 3
```

---

```csharp
supptran = new SupplierTransaction();
supptran.GLDebitPosting += new TransactionBase.GLPostingEventHandler(supptran_GLDebitPosting);
supptran.GLCreditPosting += new TransactionBase.GLPostingEventHandler(supptran_GLCreditPosting);
supptran.Account = new Supplier("Supplier1");
supptran.Amount = 70;
supptran.TaxRate = new TaxRate(7);
supptran.Reference = supptran.Description = "inv3";
supptran.TransactionCode = new TransactionCode(Module.AP, "IN");
supptran.Post();
_batch.Post();
DatabaseContext.CommitTran();
}
catch (Exception ex)
{
DatabaseContext.RollbackTran();
MessageBox.Show(ex.Message);
}
}
static void supptran_GLCreditPosting(TransactionBase sender, TransactionBase.GLPostingEventArgs e)
{
_batch.Add((GLTransaction)e.GLTransaction.Clone());
e.Posted = true;
}
static void supptran_GLDebitPosting(TransactionBase sender, TransactionBase.GLPostingEventArgs e)
{
_batch.Add((GLTransaction)e.GLTransaction.Clone(), true, true);
e.Posted = true;
}
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=CSupplierTransaction&oldid=122"

This page was last modiﬁed on 23 October 2015, at 10:57.
