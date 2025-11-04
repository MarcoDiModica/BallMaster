using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading;
using System.Diagnostics;
using System;
using System.Collections.Concurrent;

public class BubbleSort : MonoBehaviour
{
    float[] array;
    List<GameObject> mainObjects;
    public GameObject prefab;

    Thread bubbleThread;
    Thread quickThread;

    bool bubbleSorting = false;
    bool quickSorting = false;

    bool bubbleDone = false;
    bool quickDone = false;

    Stopwatch stopwatch = new Stopwatch();

    void Start()
    {
        mainObjects = new List<GameObject>();
        array = new float[30000];
        for (int i = 0; i < 30000; i++)
        {
            array[i] = (float)UnityEngine.Random.Range(0, 1000)/100;
        }

        spawnObjs();
        updateHeights();
        logArray();

        bubbleThread = new Thread(() =>
        {
            bubbleSorting = true;
            stopwatch.Restart();
            bubbleSort();
            stopwatch.Stop();
            UnityEngine.Debug.Log("Bubble Sort Time: " + stopwatch.ElapsedMilliseconds + " ms");
            bubbleSorting = false;
            bubbleDone = true;
        });
        bubbleThread.Start();

        quickThread = new Thread(() =>
        {
            quickSorting = true;
            float[] copy = (float[])array.Clone();
            stopwatch.Restart();
            quickSort(copy, 0, copy.Length - 1);
            stopwatch.Stop();
            stopwatch.Stop();
            UnityEngine.Debug.Log("Quick Sort Time: " + stopwatch.ElapsedMilliseconds + " ms");
            quickSorting = false;
            quickDone = true;
        });
        quickThread.Start();

    }

    void Update()
    {
        bool changed = updateHeights();

        if (bubbleSorting || quickSorting)
        {
            UnityEngine.Debug.Log($"Sorting... (Bubble: {bubbleSorting}, Quick: {quickSorting})");
        }
    
        if (bubbleDone)
        {
            bubbleDone = false;
            UnityEngine.Debug.Log("Bubble Sort Completed and heights updated"); 
        }

        if (quickDone)
        {
            quickDone = false;
            UnityEngine.Debug.Log("Quick Sort Completed (no height update)");
        }
    }

    void bubbleSort()
    {
        int i, j;
        int n = array.Length;
        bool swapped;
        for (i = 0; i < n- 1; i++)
        {
            swapped = false;
            for (j = 0; j < n - i - 1; j++)
            {
                if (array[j] > array[j + 1])
                {
                    (array[j], array[j+1]) = (array[j+1], array[j]);
                    swapped = true;
                }
            }
            if (swapped == false)
                break;
        }
    }

    void quickSort(float[] arr, int low, int high)
    {
        if (low<high)
        {
            int pivot = partition(arr,low, high);
            quickSort(arr, low, pivot - 1);
            quickSort(arr, pivot + 1, high);
        }
    }

    int partition(float[] arr, int low, int high)
    {
        float pivot = arr[high];
        int i = (low - 1);
        for (int j = low; j < high; j++)
        {
            if (arr[j] < pivot)
            {
                i++;
                (arr[i], arr[j]) = (arr[j], arr[i]);
            }
        }
        (arr[i + 1], arr[high]) = (arr[high], arr[i + 1]);
        return i + 1;
    }

    void logArray()
    {
        string text = "";

        for (int i=0; i < Math.Min(array.Length, 10); i++)
        {
            text += array[i].ToString("F2") + " ";
        }

        UnityEngine.Debug.Log("Array Sample:" + text);
    }
    
    void spawnObjs()
    {
        mainObjects.Clear();

        for (int i = 0; i < array.Length; i++)
        {
            GameObject obj = Instantiate(
                prefab, 
                new Vector3((float)i / 1000, this.transform.position.y, 0),
                Quaternion.identity
            );
            obj.transform.localScale = new Vector3(0.001f, array[i], 0.001f);
            mainObjects.Add(obj);
        }

    }

    bool updateHeights()
    {
        bool changed = false;
        if (mainObjects == null || mainObjects.Count == 0) return false;

        int count = Mathf.Min(mainObjects.Count, array.Length);

        for (int i = 0; i < count; i++)
        {
            GameObject obj = mainObjects[i];
            if (obj == null) continue;

            Vector3 currentScale = obj.transform.localScale;
            if (!Mathf.Approximately(currentScale.y, array[i]))
            {
                obj.transform.localScale = new Vector3(currentScale.x, array[i], currentScale.z);
                changed = true;
            }
        }
        return changed;
    }
}
