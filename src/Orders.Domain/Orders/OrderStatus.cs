using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Orders.Domain.Orders;

public enum OrderStatus
{    
    Started, // New
    Reserved, // Inventory reserved
    PaymentInProcess, // Payment in process
    Paid,// Payment completed
    Completed, // Order delivered and completed
    Canceled, // Order canceled
    Failed, // 
    Draft, // Sales note being written by the advisor: no stock reserved, no charge order
    Discarded // Draft thrown away (by the advisor or because it expired)
}
