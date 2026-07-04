using UnityEngine;

public class PlayDataManager : ManagerBase<PlayDataManager>
{
    private int _money;
    public int Money => _money;

    private int _wood;
    public int Wood => _wood;

    private int _ore;
    public int Ore => _ore;

    public void SaveMoneyChange(int amount)
    {
        _money += amount;
        // TODO: データ保存（クラウド）
    }
    
    public void SaveWoodChange(int amount)
    {
        _wood += amount;
        // TODO: データ保存（クラウド）
    }
    
    public void SaveOreChange(int amount)
    {
        _ore += amount;
        // TODO: データ保存（クラウド）
    }
}
