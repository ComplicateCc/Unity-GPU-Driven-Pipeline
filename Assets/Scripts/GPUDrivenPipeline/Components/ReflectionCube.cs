using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MPipeline
{
    public class ReflectionCube : MonoBehaviour
    {
        public static List<ReflectionCube> allCubes = new List<ReflectionCube>(100);
        public float intensity = 1;
        public float importance = 1;
        public bool useBoxProjection = false;
        public Cubemap reflectionCube;
        private int currentIndex;
        public Matrix4x4 localToWorld;
        private void OnEnable()
        {
            currentIndex = allCubes.Count;
            allCubes.Add(this);
            localToWorld = transform.localToWorldMatrix;
        }

        private void OnDisable()
        {
            if (allCubes.Count <= 1)
            {
                allCubes.Clear();
                return;
            }
            int last = allCubes.Count - 1;
            allCubes[currentIndex] = allCubes[last];
            allCubes[currentIndex].currentIndex = currentIndex;
            allCubes.RemoveAt(last);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawMesh(GraphicsUtility.cubeMesh, transform.position, transform.rotation, transform.localScale);
        }

        private void OnDrawGizmos()
        {
            Gizmos.DrawWireMesh(GraphicsUtility.cubeMesh, transform.position, transform.rotation, transform.localScale);
        }

    }
}