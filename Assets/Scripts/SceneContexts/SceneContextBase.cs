using UnityEngine;

/// <summary>
/// シーンコンテクストクラスが継承する基底クラス
/// シーン内で使い捨てのシングルトンパターンを実装する
/// シーン内のSystemインスタンスへのエントリーポイントが主な役割
/// </summary>
/// <typeparam name="T">継承先のクラスをジェネリック型に入れる</typeparam>
public abstract class SceneContextBase<T> : MonoBehaviour where T : SceneContextBase<T>
{
    public static T Instance { get; private set; }

    /// <summary>
    /// 継承先では Awake を定義せず、OnAwakeInitialize を使用してください
    /// </summary>
    private void Awake()
    {
        // シーン内使い捨てのシングルトン
        if (Instance == null)
        {
            Instance = this as T;
            OnAwakeInitialize();
        }
        else
        {
            // 既に存在する場合は自身を破棄
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