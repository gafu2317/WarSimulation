using UnityEngine;

/// <summary>
/// マネージャークラスが継承する基底クラス
/// シーン遷移時にも破棄されないシングルトンパターンを実装する
/// </summary>
/// <typeparam name="T">継承先のクラスをジェネリック型に入れる</typeparam>
public abstract class ManagerBase<T> : MonoBehaviour where T : ManagerBase<T>
{
    public static T Instance { get; private set; }

    /// <summary>
    /// 継承先では Awake を定義せず、OnAwakeInitialize を使用してください
    /// </summary>
    private void Awake()
    {
        // シーン間で保護されるシングルトン
        if (Instance == null)
        {
            Instance = this as T;
            DontDestroyOnLoad(gameObject);
            OnAwakeInitialize();
        }
        else
        {
            // 既に存在する場合は自身を無効化してから破棄
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// シングルトン確定後に呼ばれる初期化メソッド
    /// Awake の代わりにこちらをオーバーライドしてください
    /// </summary>
    protected virtual void OnAwakeInitialize() { }

    protected virtual void OnDestroy()
    {
        // 自分自身が現在のインスタンスである場合のみnullにする
        if (Instance == this as T)
        {
            Instance = null;
        }
    }
}