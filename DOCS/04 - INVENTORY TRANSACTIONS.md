# C Credit Note

### From Sage Evolution SDK | Documentation Portal

A Credit Note can be Saved or processes just like in Evolution. The following example is on placing a CN.

```csharp
CreditNote CN = new CreditNote();
CN.Customer = new Customer("Customer1");
CN.InvoiceDate = DateTime.Now;// choose to set the invoice date or Order date etc
CN.InvoiceTo = CN.Customer.PostalAddress.Condense();//Condense method can be used or you can specify
CN.DeliverTo = new Address("Physical Address 1", "Address 2", "Address 3", "Address 4", "Address 5",
CN.Project = new Project("P1");//Various CN properties like project can be set
OrderDetail OD = new OrderDetail();
CN.Detail.Add(OD);
//Vaious Order Detail properties can be added like warehouse , sales reps , userfields etc
OD.InventoryItem = new InventoryItem("ItemA");//Use the inventoryItem constructor to specify a Item
OD.Quantity = 10;
OD.ToProcess = OD.Quantity;
OD.UnitSellingPrice = 20;
OD = new OrderDetail();
CN.Detail.Add(OD);
OD.GLAccount = new GLAccount("Accounting Fees");//Use the GLAccount Item constructor to specify a Ac
OD.Quantity = 1;
OD.ToProcess = OD.Quantity;
OD.UnitSellingPrice = 30;
CN.Process();
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Credit_Note&oldid=209"

This page was last modified on 17 November 2015, at 09:37.

---

# C inventory item

### From Sage Evolution SDK | Documentation Portal

The following example is on creating a Inventory Item.

```csharp
//Create a instance of the InventoryItem Class
InventoryItem invItem = new InventoryItem();
invItem.Code = "TestSDK9";
invItem.Description = "TestSDK9";
invItem.Description_2 = "Description2";
invItem.Description_3 = "Description3";
invItem.IsWarehouseTracked = true;// Properties like whther the item is a warehouse item cam be specified
//Prices can also be specified as follows
PriceList p1 = new PriceList("Price List 1");
PriceList p2 = new PriceList("Price List 2");
PriceList p3 = new PriceList("Price List 3");
PriceList p4 = new PriceList("New Price 4");
invItem.SellingPrices[p1].PriceExcl = 200;
invItem.SellingPrices[p2].PriceExcl = 300;
invItem.SellingPrices[p3].PriceExcl = 400;
invItem.SellingPrices[p4].PriceExcl = 500;
//Save the Item
invItem.Save();
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Inventory_Item&oldid=135"

This page was last modified on 23 October 2015, at 12:02.

---

# C Inventory Transactions

### From Sage Evolution SDK | Documentation Portal

The InventorTransaction class is the equivalent of a Inventory Adjustment. Stock can be increased
decreased or the cost adjusted.

```csharp
//Create a instance of the InventoryTransaction class
InventoryTransaction ItemInc = new InventoryTransaction();
ItemInc.TransactionCode = new TransactionCode(Module.Inventory, "ADJ");// specify a inventory transa
ItemInc.InventoryItem = new InventoryItem("Item1");
ItemInc.Operation = InventoryOperation.Increase;//Select the necessary enumerator increase , decreas
ItemInc.Quantity = 2;
ItemInc.Reference = "F2";
ItemInc.Reference2 = "ref2";
ItemInc.Description = "desc";
ItemInc.Post();
//Create a instance of the InventoryTransaction class
InventoryTransaction ITCost = new InventoryTransaction();
ITCost.TransactionCode = new TransactionCode(Module.Inventory, "ADJ");// specify a inventory transac
ITCost.InventoryItem = new InventoryItem("Item1");
ITCost.Operation = InventoryOperation.CostAdjustment;//Select the necessary enumerator increase , de
ITCost.UnitCost = 75;
ITCost.Reference = "F2";
ITCost.Reference2 = "ref2";
ITCost.Description = "desc";
ITCost.Post();
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Inventory_Transactions&oldid=136"

This page was last modified on 23 October 2015, at 12:03.

---

# C Return To Suppliers

### From Sage Evolution SDK | Documentation Portal

A RTS can be Saved or processes just like in Evolution. The following example is on placing a Return to
supplier.

```csharp
ReturnToSupplier RTS = new ReturnToSupplier();
RTS.Supplier = new Supplier("SupplierSDK1");
RTS.InvoiceDate = DateTime.Now;// choose to set the invoice date or Order date etc
RTS.InvoiceTo = RTS.Supplier.PostalAddress.Condense();//Condense method can be used or you can specify the addre
RTS.DeliverTo = new Address("Physical Address 1", "Address 2", "Address 3", "Address 4", "Address 5", "PC");
RTS.Project = new Project("P1");//Various RTS properties like project can be set
OrderDetail OD = new OrderDetail();
RTS.Detail.Add(OD);
//Vaious Order Detail properties can be added like warehouse , sales reps , userfields etc
OD.InventoryItem = new InventoryItem("ItemA");//Use the inventoryItem constructor to specify a Item
OD.Quantity = 10;
OD.ToProcess = OD.Quantity;
OD.UnitSellingPrice = 20;
OD = new OrderDetail();
RTS.Detail.Add(OD);
OD.GLAccount = new GLAccount("Accounting Fees");//Use the GLAccount Item constructor to specify a Account
OD.Quantity = 1;
OD.ToProcess = OD.Quantity;
OD.UnitSellingPrice = 30;
RTS.Process();
```

The following example displays how to specify Additional Costs on a Return to supplier Document:

```csharp
ReturnToSupplier RTS = new ReturnToSupplier();
RTS.Supplier = new Supplier("SupplierSDK1");
//Additional costs can be specified on a Return to supplier like the functionality in Evolution
CostAllocation costAlloc = new CostAllocation();
costAlloc.Supplier = new Supplier("Supplier1");
costAlloc.Reference = "ADDCost";
costAlloc.Description = "AddCost";
costAlloc.Amount = 200;
costAlloc.TaxRateID = 3;
costAlloc.Save();// Save the additional cost lines
RTS.AdditionalCosts.Add(costAlloc);//Add the total additional costs to the RTS
OrderDetail OD = new OrderDetail();
RTS.Detail.Add(OD);
OD.InventoryItem = new InventoryItem("ItemA");
OD.TotalAdditionalCost = 100;// Specify the Additional Costs per line or remove and use the distribute method be
OD.Quantity = 1;
OD.ToProcess = OD.Quantity;
OD.UnitSellingPrice = 20;
OD = new OrderDetail();
RTS.Detail.Add(OD);
OD.InventoryItem = new InventoryItem("ItemB");
OD.TotalAdditionalCost = 100;
OD.Quantity = 1;
OD.ToProcess = OD.Quantity;
OD.UnitSellingPrice = 30;
// RTS.AdditionalCosts._Distribute();// Use this method when not specifying additional costs per line and to dis
RTS.Process();
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Return_To_Suppliers&oldid=137"

---

This page was last modified on 23 October 2015, at 12:04.

---

# C Warehouse Inter Branch Transfer

### From Sage Evolution SDK | Documentation Portal

From Version 8.00... onwards the SDK has functionality to do a Warehouse IBT Transfer. To do
Warehouse IBT Transfers the feature has to be enabled in Evolution Warehouse Defaults.

```csharp
// The following will be the issueing of stock from one warehouse to another leaving the stock in transit
WarehouseIBT IBTIssue = new WarehouseIBT();
IBTIssue.WarehouseFrom = new Warehouse("W1");//Specify From which warehouse qty will be transfered from
IBTIssue.WarehouseTo = new Warehouse("W2");//Specify To which warehouse qty will be transfered to
IBTIssue.Description = "Test1des";
WarehouseIBTLine IBTIssueLine = new WarehouseIBTLine();
IBTIssueLine.InventoryItem = new InventoryItem("ItemwA");
IBTIssueLine.Description = "testline1";
IBTIssueLine.Reference = "Ref001";
IBTIssueLine.QuantityIssued = 5;
IBTIssue.Detail.Add(IBTIssueLine);
IBTIssue.IssueStock();
//The following will be the receiving of stock for the issue above that is in transit
WarehouseIBT IBTReceive = new WarehouseIBT(IBTIssue.Number);
foreach (WarehouseIBTLine IBTReceiveLine in IBTReceive.Detail)
{
IBTReceiveLine.QuantityReceived = 2;
}
IBTReceive.ReceiveStock();
```

### Retrieved from "https://developerzone.pastel.co.za/index.php?

### title=C_Warehouse_Inter_Branch_Transfer&oldid=317"

This page was last modified on 18 July 2017, at 13:26.

---

# C Warehouse Transfer

### From Sage Evolution SDK | Documentation Portal

The SDK does not support Warehouse IBT Transfers prior to Evolution Version 8.00...(Sage 100).
However it is possible to do Warehouse Transfers.

```csharp
//Create a instance of the WareouseTransfer Class
WarehouseTransfer WT = new WarehouseTransfer();
WT.Account = new InventoryItem("Itemw1");//specify the Item to transfer
WT.FromWarehouse = new Warehouse("w1");//Specify the From Warehouse
WT.ToWarehouse = new Warehouse("w2");//specify the TO Warehouse
WT.Quantity = 1;//Specify the Quantity to transfer
WT.Reference = "ref1";
WT.Reference2 = "ref2";
//Post the warehouse transfer
WT.Post();
```

Retrieved from "https://developerzone.pastel.co.za/index.php?title=C_Warehouse_Transfer&oldid=316"

This page was last modified on 18 July 2017, at 13:17.
