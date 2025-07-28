using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox
{
    public class GPUInstancingManager : MonoBehaviour
    {

        [System.Serializable]
        public class ObjectData
        {
            public string groupName;
            public Material material;
            public Mesh mesh;

            public ObjectData(string group, Material mat, Mesh m)
            {
                groupName = group;
                material = mat;
                mesh = m;
            }

            // ตรวจสอบว่า ObjectData ตรงกันหรือไม่ (mesh และ material เหมือนกัน)
            public bool Matches(Material mat, Mesh m)
            {
                return material == mat && mesh == m;
            }
        }




        [System.Serializable]
        public class InstanceData
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public bool isVisible;

            public InstanceData(Vector3 pos, Quaternion rot, Vector3 scl, bool visible)
            {
                position = pos;
                rotation = rot;
                scale = scl;
                isVisible = visible;
            }

            // Method สำหรับอัพเดทตำแหน่ง
            public void UpdateTransform(Vector3 pos, Quaternion rot, Vector3 scl)
            {
                position = pos;
                rotation = rot;
                scale = scl;
            }

            // Method สำหรับเปลี่ยนการมองเห็น
            public void SetVisible(bool visible)
            {
                isVisible = visible;
            }

            // Method สำหรับดึง Matrix4x4
            public Matrix4x4 GetMatrix()
            {
                return Matrix4x4.TRS(position, rotation, scale);
            }
        }

        [SerializeField] private List<ObjectData> objectTypes = new List<ObjectData>();
        private Dictionary<string, List<InstanceData>> instanceGroups; // ใช้ groupName แทน ObjectData
        private Dictionary<string, ObjectData> groupObjectData; // เก็บข้อมูล ObjectData ของแต่ละ group
        private MaterialPropertyBlock propertyBlock;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            instanceGroups = new Dictionary<string, List<InstanceData>>();
            groupObjectData = new Dictionary<string, ObjectData>();
            propertyBlock = new MaterialPropertyBlock();

            // Initialize groups for each object type
            foreach (var objectData in objectTypes)
            {
                if (!string.IsNullOrEmpty(objectData.groupName))
                {
                    instanceGroups[objectData.groupName] = new List<InstanceData>();
                    groupObjectData[objectData.groupName] = objectData;
                }
            }
        }

        public InstanceData CreateInstance(string groupName, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return CreateInstanceInternal(groupName, null, null, position, rotation, scale);
        }

        public InstanceData CreateInstance(ObjectData objectData, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            return CreateInstanceInternal(objectData.groupName, objectData.material, objectData.mesh, position, rotation, scale);
        }

        public InstanceData CreateInstance(Material material, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // หาหรือสร้าง group สำหรับ material และ mesh นี้
            string groupName = FindOrCreateGroup(material, mesh);
            return CreateInstanceInternal(groupName, material, mesh, position, rotation, scale);
        }

        private string FindOrCreateGroup(Material material, Mesh mesh)
        {
            // หา group ที่มี material และ mesh ตรงกัน
            foreach (var kvp in groupObjectData)
            {
                if (kvp.Value.Matches(material, mesh))
                {
                    return kvp.Key;
                }
            }

            // ถ้าไม่เจอ สร้างใหม่
            string newGroupName = $"AutoGroup_{material.name}_{mesh.name}_{Time.realtimeSinceStartup}";
            ObjectData newObjectData = new ObjectData(newGroupName, material, mesh);

            instanceGroups[newGroupName] = new List<InstanceData>();
            groupObjectData[newGroupName] = newObjectData;

            return newGroupName;
        }

        private InstanceData CreateInstanceInternal(string groupName, Material material, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // ตรวจสอบว่า group อยู่ในระบบหรือไม่
            if (!instanceGroups.ContainsKey(groupName))
            {
                if (material != null && mesh != null)
                {
                    Debug.LogWarning($"Group '{groupName}' not found. Creating new group.");
                    ObjectData newObjectData = new ObjectData(groupName, material, mesh);
                    instanceGroups[groupName] = new List<InstanceData>();
                    groupObjectData[groupName] = newObjectData;
                }
                else
                {
                    Debug.LogError($"Group '{groupName}' not found and no material/mesh provided to create it.");
                    return null;
                }
            }

            // สร้าง InstanceData ใหม่
            InstanceData newInstance = new InstanceData(position, rotation, scale, true);

            // เพิ่มเข้าไปในกลุ่ม
            instanceGroups[groupName].Add(newInstance);

            return newInstance;
        }

        public bool RemoveInstance(string groupName, InstanceData instance)
        {
            if (instanceGroups.ContainsKey(groupName))
            {
                return instanceGroups[groupName].Remove(instance);
            }
            return false;
        }

        public void ClearInstances(string groupName)
        {
            if (instanceGroups.ContainsKey(groupName))
            {
                instanceGroups[groupName].Clear();
            }
        }

        public void ClearAllInstances()
        {
            foreach (var group in instanceGroups.Values)
            {
                group.Clear();
            }
        }

        public int GetInstanceCount(string groupName)
        {
            if (instanceGroups.ContainsKey(groupName))
            {
                return instanceGroups[groupName].Count;
            }
            return 0;
        }

        // ฟังก์ชันใหม่: ดึง instances ตาม group
        public List<InstanceData> GetInstancesByGroup(string groupName)
        {
            if (instanceGroups.ContainsKey(groupName))
            {
                return new List<InstanceData>(instanceGroups[groupName]); // Return copy to prevent external modification
            }
            return new List<InstanceData>(); // Return empty list if group not found
        }

        // ฟังก์ชันดึงชื่อ groups ทั้งหมด
        public List<string> GetAllGroupNames()
        {
            return new List<string>(instanceGroups.Keys);
        }

        private void Update()
        {
            RenderAllInstances();
        }

        private void RenderAllInstances()
        {
            foreach (var kvp in instanceGroups)
            {
                string groupName = kvp.Key;
                List<InstanceData> instances = kvp.Value;

                if (instances.Count == 0 || !groupObjectData.ContainsKey(groupName))
                    continue;

                ObjectData objectData = groupObjectData[groupName];
                if (objectData.material == null || objectData.mesh == null)
                    continue;

                RenderInstanceGroup(objectData, instances);
            }
        }

        private void RenderInstanceGroup(ObjectData objectData, List<InstanceData> instances)
        {
            // สร้าง array ของ matrices สำหรับ instances ที่มองเห็นได้
            List<Matrix4x4> visibleMatrices = new List<Matrix4x4>();

            foreach (var instance in instances)
            {
                if (instance.isVisible)
                {
                    visibleMatrices.Add(instance.GetMatrix());
                }
            }

            if (visibleMatrices.Count == 0) return;

            // แปลงเป็น array
            Matrix4x4[] matricesArray = visibleMatrices.ToArray();

            // Unity's GPU instancing can handle up to 1023 instances per draw call
            int batchSize = 1023;
            int batches = Mathf.CeilToInt((float)matricesArray.Length / batchSize);

            for (int batch = 0; batch < batches; batch++)
            {
                int startIndex = batch * batchSize;
                int endIndex = Mathf.Min(startIndex + batchSize, matricesArray.Length);
                int currentBatchSize = endIndex - startIndex;

                Matrix4x4[] batchMatrices = new Matrix4x4[currentBatchSize];
                System.Array.Copy(matricesArray, startIndex, batchMatrices, 0, currentBatchSize);

                Graphics.DrawMeshInstanced(
                    objectData.mesh,
                    0,
                    objectData.material,
                    batchMatrices,
                    currentBatchSize,
                    propertyBlock
                );
            }
        }
    }

}