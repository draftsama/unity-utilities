using UnityEngine;

namespace Sandbox{
    public class test : MonoBehaviour
    {
        public GPUInstancingManager.ObjectData objectData1;
        public GPUInstancingManager.ObjectData objectData2;



        public GPUInstancingManager manager;
        public GPUInstancingManager.InstanceData instanceData1;
        public GPUInstancingManager.InstanceData instanceData2;
        private void Start()
        {

            instanceData1 = manager.CreateInstance(
                objectData1,
                new Vector3(0, 0, 0),
                Quaternion.identity,
                new Vector3(1, 1, 1)
            );
            instanceData2 = manager.CreateInstance(
                objectData2,
                new Vector3(3, 0, 0),
                Quaternion.identity,
                new Vector3(1, 1, 1)
            );

        }

        private void Update()
        {
            if (instanceData1 != null)
            {
                //ping-pong
                var y = Mathf.PingPong(Time.time, 2) - 1;
                instanceData1.UpdateTransform(
                     new Vector3(0, y, 0),
                     Quaternion.identity,
                     new Vector3(1, 1, 1)
                 );
            }

            if (instanceData2 != null)
            {
                //ping-pong
                var y = Mathf.PingPong(Time.time, 2) - 1;
                instanceData2.UpdateTransform(
                     new Vector3(3, y, 0),
                     Quaternion.identity,
                     new Vector3(1, 1, 1)
                 );
            }

        }

    }


}
