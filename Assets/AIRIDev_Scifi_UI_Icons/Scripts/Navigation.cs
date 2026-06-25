using UnityEngine;
namespace Scifi_UI_Icons
{
public class Navigation : MonoBehaviour
{
    public GameObject[] objects;

    private int currentIndex = 0;

    void Start()
    {
        // Make sure only the first object is active at start
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i].SetActive(i == 0);
        }
    }

    public void ActivateNext()
    {
        if (objects.Length == 0) return;

        // Deactivate current
        objects[currentIndex].SetActive(false);

        // Move to next index
        currentIndex++;

        // Loop back if reached end
        if (currentIndex >= objects.Length)
            currentIndex = 0;

        // Activate new current
        objects[currentIndex].SetActive(true);
    }
}
}
