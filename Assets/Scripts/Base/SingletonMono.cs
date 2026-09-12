using UnityEngine;
using System.Collections;

public abstract class SingletonMonoBase : MonoBehaviour
{
    void Awake()
    {
        if (!BeforeAwakeMessage())
            return;

        OnAwake();
    }

    void Start()
    {
        OnStart();
    }

    void Update()
    {
        OnUpdate();
    }

    void OnDestroy()
    {
        BeforeOnDestroy();
        BeforeDestroyMessage();
    }

    void OnApplicationQuit()
    {
        OnApplicationQuitMessage();
    }

    protected virtual bool BeforeAwakeMessage() { return true; }
    protected virtual void BeforeDestroyMessage() { }
    protected virtual void OnApplicationQuitMessage() { }
    protected virtual void OnAwake() { }
    protected virtual void OnStart() { }
    protected virtual void OnUpdate() { }
    protected virtual void BeforeOnDestroy() { }
}

public abstract class SingletonMono<T> : SingletonMonoBase where T : MonoBehaviour
{
    #region 单例
    private static T instance;
    private static bool applicationIsQuitting;

    public static T Instance
    {
        get
        {
            if (applicationIsQuitting)
                return null;

            if (instance == null)
            {
                GameObject obj = new GameObject(typeof(T).Name);
                DontDestroyOnLoad(obj);

                instance = obj.GetComponent<T>();
                if(instance==null)
                    instance = obj.AddComponent<T>();

            }
            return instance;
        }
    }

    public static bool TryGetInstance(out T existingInstance)
    {
        existingInstance = instance;
        return existingInstance != null;
    }
    #endregion

    protected override bool BeforeAwakeMessage()
    {
        if (instance == null)
        {
            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (!ReferenceEquals(instance, this))
        {
            Destroy(gameObject);
            return false;
        }

        return true;
    }

    protected override void BeforeDestroyMessage()
    {
        if (ReferenceEquals(instance, this))
            instance = null;
    }

    protected override void OnApplicationQuitMessage()
    {
        applicationIsQuitting = true;
    }
}
