using UnityEngine;

public abstract class SceneContextBase<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance { get; private set; }

    protected virtual void Awake()
    {   
        // シーン内使い捨てのシングルトン
        if (Instance == null)
        {
            Instance = this as T;
        }
        else
        {
            // 既に存在する場合は自身を破棄
            Destroy(this); 
        }
    }

    protected virtual void OnDestroy()
    {
        // 自分自身が現在のインスタンスである場合のみnullにする
        if (Instance == this as T)
        {
            Instance = null;
        }
    }
}