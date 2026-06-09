21/07/2024, 22:04 C Cashbook Batches - Sage Evolution SDK | Documentation Portal

# C Cashbook Batches

## From Sage Evolution SDK | Documentation Portal

## Cashbook Batches can be created and populated by the SDK. However they cannot be processed through the

## SDK.

## The following example displays how to create and save a Cashbook Batch.

{
string BatchNum = "CB001";
//check if Batch code exists in pastel
if (CashbookBatch.FindByCode(BatchNum) == -1)
{
// Batch code does not exist, so create it
CashbookBatch createbatch = new CashbookBatch();
createbatch.Code = BatchNum;
createbatch.Description = "CB Batch";
createbatch.Owner = new Agent("Admin");
createbatch.Save();
}

CashbookBatch batch = new CashbookBatch(BatchNum);

CashbookBatchDetail detail = new CashbookBatchDetail();
detail.Date = DateTime.Today;
detail.LineModule = CashbookBatchDetail.Module.Ledger;//Specify the line module
detail.Account = new GLAccount("Advertising");
detail.Credit = 500;
detail.Description = "Ledger Transaction";
detail.Reference = "Ref001";
detail.Tax = 50;
detail.TaxType = new TaxRate(1);//Specify a tax type
detail.TaxAccount = new GLAccount("Accruals");//Specify a tax account
batch.Detail.Add(detail);
batch.Save();//Save each CashbookBatchDetail seperately or comment out to save all together

detail = new CashbookBatchDetail();
detail.Date = DateTime.Today;
detail.LineModule = CashbookBatchDetail.Module.Payables;
detail.Supplier = new Supplier("Supplier1");
detail.Debit = 500;
detail.Description = "Supplier Transaction";
detail.Reference = "Ref001";
batch.Detail.Add(detail);
batch.Save();

detail = new CashbookBatchDetail();
detail.Date = DateTime.Today;
detail.LineModule = CashbookBatchDetail.Module.Receivables;
detail.Customer = new Customer("Customer1");
detail.Debit = 500;
detail.Description = "Customer Transaction";
detail.Reference = "Ref001";
batch.Detail.Add(detail);
batch.Save();

//If required the clone method can be used to copy the previous line
batch = new CashbookBatch(batch.Code);
batch.Detail.Add(detail.Clone());
batch.Save();
}

## Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Cashbook_Batches&oldid=133"

## This page was last modiﬁed on 23 October 2015, at 12:00.

https://developerzone.pastel.co.za/index.php?title=C_Cashbook_Batches 1/1

---

21/07/2024, 22:03 C General Ledger Accounts - Sage Evolution SDK | Documentation Portal

# C General Ledger Accounts

## From Sage Evolution SDK | Documentation Portal

## The supplier class is used to create General Ledger Accounts which can be used on Ledger related

## processes.

## The following code displays how a new GL Account is created with the basic necessary properties like

## Code and Account Type speciﬁed.

//Assign variable gl to GLAccount class
GLAccount gl = new GLAccount();
//Specify Account properties like code and Account Type
gl.Code = "SDKAccount";
gl.Type = GLAccount.AccountType.CurrentAsset;
//Use the save method to Save the Account
gl.Save();

## Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_General_Ledger_Accounts&oldid=131"

## This page was last modiﬁed on 23 October 2015, at 11:57.

https://developerzone.pastel.co.za/index.php?title=C_General_Ledger_Accounts 1/1

---

21/07/2024, 22:04 C General Ledger Transactions - Sage Evolution SDK | Documentation Portal

# C General Ledger Transactions

## From Sage Evolution SDK | Documentation Portal

## Ledger Transactions can be posted which is the equivalent of a Journal done in Evolution as follows:

{
DatabaseContext.BeginTran();
GLTransaction GLDr = new GLTransaction();
GLDr.Account = new GLAccount("Sales");// specify the Gl Account
GLDr.Debit = 100+14;
GLDr.Date = DateTime.Today;
GLDr.Description = "descr";
GLDr.Reference = "ref1";
GLDr.TransactionCode = new TransactionCode(Module.GL, "JNL");//Specify the GL transaction code
GLDr.Post();

GLTransaction GLCr = new GLTransaction();
GLCr.Account = new GLAccount("Accruals");// specify the Gl Account
GLCr.Credit = 100;
GLCr.TaxRate = new TaxRate(1);
//GLCr.Tax = 14;//Tax Amount can be specified if required
GLCr.Date = DateTime.Today;
GLCr.Description = "descr";
GLCr.Reference = "ref1";
GLCr.TransactionCode = new TransactionCode(Module.GL, "JNL");//Specify the GL transaction code
GLCr.Post();

//Posting the vat leg of the credit transaction above
GLTransaction Tax = new GLTransaction();
Tax.Account = new GLAccount("Vat Control");// specify the Gl Tax Account
Tax.Credit = 14;
//Tax.Tax = GLCr.Tax;
Tax.Date = DateTime.Today;
Tax.ModID = ModuleID.Tax;//Specify this transaction id to be tax
Tax.Description = "descr";
Tax.Reference = "ref1";
Tax.TransactionCode = new TransactionCode(Module.GL, "JNL");//Specify the GL transaction code
Tax.Post();
DatabaseContext.CommitTran();
}

## Retrieved from "https://developerzone.pastel.co.za/index.php?

## title=C_General_Ledger_Transactions&oldid=132"

## This page was last modiﬁed on 23 October 2015, at 11:58.

https://developerzone.pastel.co.za/index.php?title=C_General_Ledger_Transactions 1/1

---

21/07/2024, 22:04 C Journal Batches - Sage Evolution SDK | Documentation Portal

# C Journal Batches

## From Sage Evolution SDK | Documentation Portal

## Journal Batches can be created and populated by the SDK. However they cannot be processed.

## The following example displays how to create and save a Journal Batch.

{
string BatchNum = "JB002";
//check if Batch code exists in pastel
if (JournalBatch.FindByCode(BatchNum) == -1)
{
// Batch code does not exist, then create it
JournalBatch createbatch = new JournalBatch();
createbatch.Code = BatchNum;
createbatch.Description = "JB Batch";
createbatch.Owner = new Agent("Admin");
createbatch.Save();
}

JournalBatch batch = new JournalBatch(BatchNum);

var detail = new JournalBatchDetail();
detail.Date = DateTime.Today;
detail.Account = new GLAccount("Advertising");
detail.Debit = 500;
detail.Description = "So and so";
detail.Reference = "Ref001";
batch.Detail.Add(detail);
batch.Save();//Save each JournalBatchDetail seperately or comment out to save both together

detail = new JournalBatchDetail();
detail.Date = DateTime.Today;
detail.Account = new GLAccount("Advertising");
detail.Credit = 500;
detail.Description = "So and so";
detail.Reference = "Ref001";
detail.Tax = 50;
detail.TaxRate = new TaxRate(1);//Specify a tax type
detail.TaxAccount = new GLAccount("Accruals");//Specify a tax account
batch.Detail.Add(detail);
batch.Save();

//If required the clone method can be used to copy the previous line
batch = new JournalBatch(batch.Code);
batch.Detail.Add(detail.Clone());
batch.Save();
}

## Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Journal_Batches&oldid=134"

## This page was last modiﬁed on 23 October 2015, at 12:01.

https://developerzone.pastel.co.za/index.php?title=C_Journal_Batches 1/1
