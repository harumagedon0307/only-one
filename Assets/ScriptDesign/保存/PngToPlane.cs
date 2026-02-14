using UnityEngine;

public class PngToPlane : MonoBehaviour
{
    public Texture2D png;

    public GameObject CreateModel()
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);

        Material mat = new Material(Shader.Find("Standard"));
        mat.mainTexture = png;
        plane.GetComponent<Renderer>().material = mat;

        return plane;
    }
}

