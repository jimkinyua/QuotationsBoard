public class AddSingleTrade
{
    public DateTime TradeDate { get; set; }
    public List<BondTradeLineDTO> Trades { get; set; } = new List<BondTradeLineDTO>();

}