using UnityEngine;
using TMPro;
using System.Collections;

public class BiomeNotificator : MonoBehaviour
{
    public TextMeshProUGUI notificationText;
    public float fadeDuration = 1f;
    public float displayDuration = 3f;

    private Coroutine notificationCoroutine;

    private void Start()
    {
        notificationText.alpha = 0;
    }

    public void ShowNotification(string biomeName)
    {
        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
        }
        notificationCoroutine = StartCoroutine(FadeNotification(biomeName));
    }

    private IEnumerator FadeNotification(string biomeName)
    {
        notificationText.text = "You have discovered the " + biomeName;

        // Fade in
        float elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            notificationText.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }

        // Hold
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        elapsedTime = 0f;
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            notificationText.alpha = 1f - Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }
    }
}
