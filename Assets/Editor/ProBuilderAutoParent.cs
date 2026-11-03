using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[InitializeOnLoad]
public static class ProBuilderAutoParent
{
    // Список для отслеживания уже обработанных объектов
    private static HashSet<int> processedObjects = new HashSet<int>();

    static ProBuilderAutoParent()
    {
        // Подписываемся на любое изменение иерархии сцены в редакторе
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
    }

    private static void OnHierarchyChanged()
    {
        // Находим все корневые объекты на сцене (ProBuilder всегда спавнит их в корень)
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

        foreach (GameObject obj in rootObjects)
        {
            int instanceID = obj.GetInstanceID();

            // Если мы этот объект еще не проверяли
            if (!processedObjects.Contains(instanceID))
            {
                // Ищем на объекте компонент ProBuilder по точному имени его класса из новой версии
                Component pbShape = obj.GetComponent("ProBuilderShape");

                // Если компонент найден — перед нами новый объект ProBuilder
                if (pbShape != null)
                {
                    processedObjects.Add(instanceID);

                    // Ищем родительский контейнер для геометрии
                    GameObject levelRoot = GameObject.Find("[LEVEL_GEOMETRY]");

                    if (levelRoot == null)
                    {
                        levelRoot = new GameObject("[LEVEL_GEOMETRY]");
                        Undo.RegisterCreatedObjectUndo(levelRoot, "Create Level Root");
                    }

                    // Переносим объект внутрь контейнера с поддержкой Ctrl+Z
                    Undo.SetTransformParent(obj.transform, levelRoot.transform, "Auto Parent ProBuilder");
                    
                    // Прерываем цикл для корректного обновления кадров редактора
                    break;
                }
            }
        }
    }
}