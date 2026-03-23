using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System;
using Unity.VisualScripting;

public class SaveManager : MonoBehaviour //常驻节点，处理保存、加载
{
    public static SaveManager instance;

    public string sceneName;



    // 存档路径
    string SaveDirectory;
    private string GetMetaPath(string saveId) => Path.Combine(SaveDirectory, $"{saveId}_meta.json");
    private string GetDataPath(string saveId) => Path.Combine(SaveDirectory, $"{saveId}_data.json");
    private string GetTempDataPath(string saveId) => Path.Combine(SaveDirectory, $"{saveId}_data.tmp");

    // 当前正在操作的存档槽ID（用于确保不同时操作同一个槽）
    private HashSet<string> savingLocks = new HashSet<string>();
    private HashSet<string> loadingLocks = new HashSet<string>();

//

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }

        var dispatcher = UnityMainThreadDispatcher.Instance; // 提前激活主线程调度器

        SaveDirectory = Path.Combine(Application.persistentDataPath, "Saves");

        if (!Directory.Exists(SaveDirectory))
        {
            Directory.CreateDirectory(SaveDirectory);
        }

        DontDestroyOnLoad(this);
    }


    // 保存游戏(异步)
    public void SaveGameAsync(string saveId, SaveGameData gamedata, Action<bool> onComplete = null)
    {
        // 检查是否正在存档、加载
        if (savingLocks.Contains(saveId) || loadingLocks.Contains(saveId))
        {
            onComplete?.Invoke(false);
            return;
        }

        savingLocks.Add(saveId);

        Task.Run(() =>
        {
            bool success = false;
            try
            {
                // 更新元数据
                var metadata = ExtractMetadata(gamedata);

                // 序列化数据
                string dataJson = JsonConvert.SerializeObject(gamedata, Formatting.Indented);
                string metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
                

                // 写入临时数据 （防止存档失败时数据损坏）
                string tempDataPath = GetTempDataPath(saveId);
                File.WriteAllText(tempDataPath, dataJson);

                // 将临时数据转为正式数据
                string dataPath = GetDataPath(saveId);
                if (File.Exists(dataPath)) File.Delete(dataPath);
                File.Move(tempDataPath, dataPath);

                // 保存元数据
                string metaPath = GetMetaPath(saveId);
                File.WriteAllText(metaPath, metadataJson);

                success = true;
            }
            catch (Exception e)
            {
                Debug.LogError("保存失败" + e);
            }

            finally
            {
                // 释放锁,在主线程回调
                savingLocks.Remove(saveId);
                UnityMainThreadDispatcher.Instance?.Enqueue(() => onComplete?.Invoke(success));
            }
        });
    }

    private SaveMetadata ExtractMetadata(SaveGameData data)
    {
        return new SaveMetadata
        {
            saveId = data.saveId,
            lastPlayTime = data.saveTime,
            sceneName = data.sceneName,
            playerLevel = data.player.level,
            playDuration = 0f // 这里演示用，实际应该从全局计时器获取
        };
    }


    // 加载游戏(异步)
    public void LoadGameAsync(string saveId, Action<SaveGameData> onComplete)
    {
        Debug.Log("LoadGameAsync called with saveId: " + saveId);

        if (loadingLocks.Contains(saveId) || savingLocks.Contains(saveId))
        {
            onComplete?.Invoke(null);
            return;
        }

        loadingLocks.Add(saveId);

        Task.Run(() =>
        {
            SaveGameData data = null;
            try
            {
                string dataPath = GetDataPath(saveId);
                if (!File.Exists(dataPath)) return;

                string dataJson = File.ReadAllText(dataPath);
                data = JsonConvert.DeserializeObject<SaveGameData>(dataJson);
            }
            catch (Exception e)
            {
                Debug.LogError("加载失败" + e);
            }
            finally
            {
                loadingLocks.Remove(saveId);
                UnityMainThreadDispatcher.Instance?.Enqueue(() => onComplete?.Invoke(data));
            }
        });
    }

    // 获取所有存档列表
    public List<SaveMetadata> GetAllSaveList()
    {
        var result = new List<SaveMetadata>();
        if (!Directory.Exists(SaveDirectory)) return result;

        var metaFiles = Directory.GetFiles(SaveDirectory, "*_meta.json");
        foreach (var metaFile in metaFiles)
        {
            try
            {
                string metaJson = File.ReadAllText(metaFile);
                var meta = JsonConvert.DeserializeObject<SaveMetadata>(metaJson);
                if (meta != null) result.Add(meta);
            }
            catch (Exception e)
            {
                Debug.LogError("读取存档元数据失败" + e);
            }
        }

        // 按时间排序
        result.Sort((a, b) => b.lastPlayTime.CompareTo(a.lastPlayTime));
        return result;
    }

    // 删除存档
    public void DeleteSave(string saveId, Action<bool> onComplete = null)
    {
        if (savingLocks.Contains(saveId) || loadingLocks.Contains(saveId))
        {
            onComplete?.Invoke(false);
            return;
        }

        Task.Run(() =>
        {
            bool success = false;
            try
            {
                string dataPath = GetDataPath(saveId);
                string metaPath = GetMetaPath(saveId);
                if (File.Exists(dataPath)) File.Delete(dataPath);
                if (File.Exists(metaPath)) File.Delete(metaPath);
                success = true;
            }
            catch (Exception e)
            {
                Debug.LogError("删除存档失败" + e);
            }
            finally
            {
                UnityMainThreadDispatcher.Instance?.Enqueue(() => onComplete?.Invoke(success));
            }
        }
);
    }
}
