namespace ERP.Domain.Inventory;

/// <summary>
/// Classifies a stock-ledger entry. Every change to on-hand quantity is one of these,
/// recorded immutably so stock can be reconstructed and audited from the ledger.
/// </summary>
public enum MovementType
{
    Receipt = 1,       // goods in (purchase/return), creates a FIFO cost layer
    Issue = 2,         // goods out (sale/consumption), consumes FIFO layers
    AdjustmentIn = 3,  // positive stock-take correction
    AdjustmentOut = 4, // negative stock-take correction
    TransferIn = 5,    // received into a warehouse from another
    TransferOut = 6    // sent out of a warehouse to another
}
