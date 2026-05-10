using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq; 

public class SpatialDigitalTwin : MonoBehaviour
{
    [Header("Server Connection")]
    public string serverIp = "192.168.100.1"; 
    public string port = "8000";
    
    [Header("Sync Settings")]
    public float lerpSpeed = 15f;
    public float rotationSensitivity = 15f;

    private string syncUrl;

    void Start() {
        syncUrl = $"http://{serverIp}:{port}/api/sync";
        StartCoroutine(SyncLoop());
    }

    IEnumerator SyncLoop() {
        while (true) {
            using (UnityWebRequest request = UnityWebRequest.Get(syncUrl)) {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success) {
                    UpdateUnityScene(request.downloadHandler.text);
                }
            }
            yield return new WaitForSeconds(0.05f); 
        }
    }

    void UpdateUnityScene(string json) {
        try {
            JObject data = JObject.Parse(json);
            JObject mappings = data["mappings"] as JObject;
            JObject tags = data["tags"] as JObject;
            JObject draggingStates = data["is_dragging"] as JObject;
            JArray assets = data["assets"] as JArray;

            if (mappings == null || tags == null) return;

            foreach (var mapping in mappings) {
                string tagId = mapping.Key;
                int assetIndex = (int)mapping.Value;
                string targetName = (string)assets[assetIndex]["name"];
                GameObject targetObj = GameObject.Find(targetName);

                if (targetObj != null && tags.ContainsKey(tagId)) {
                    bool isDragging = (bool)draggingStates[tagId];
                    float tagX = (float)tags[tagId]["x"];
                    float tagZ = (float)tags[tagId]["z"];
                    float tagY = targetObj.transform.position.y; 

                    Vector3 realWorldPos = new Vector3(tagX, tagY, tagZ);
                    
                    // 해당 오브젝트가 사람 캐릭터인지(HardwareTagFollower가 있는지) 확인
                    HardwareTagFollower follower = targetObj.GetComponent<HardwareTagFollower>();

                    if (isDragging) {
                        if (follower != null) {
                            // 🚶 [사람 캐릭터 처리] 
                            // 목표 위치만 넘겨주면 걷기 애니메이션과 회전은 Follower가 알아서 자연스럽게 처리함
                            follower.UpdateHardwarePosition(realWorldPos);
                        } 
                        else {
                            // 📦 [일반 사물 처리 (상자, 소화기 등)] 
                            // 부드러운 위치 이동
                            targetObj.transform.position = Vector3.Lerp(targetObj.transform.position, realWorldPos, Time.deltaTime * lerpSpeed);

                            // 사물은 MPU 센서의 기울기(ax, az)를 그대로 적용하여 회전
                            float ax = (float)tags[tagId]["ax"];
                            float az = (float)tags[tagId]["az"];
                            Quaternion targetRotation = Quaternion.Euler(ax * rotationSensitivity, 0, az * rotationSensitivity);
                            targetObj.transform.rotation = Quaternion.Lerp(targetObj.transform.rotation, targetRotation, Time.deltaTime * lerpSpeed);
                        }
                    }
                    else {
                        // 버튼을 뗐을 때 (멈춤 상태 처리)
                        if (follower != null) {
                            // 캐릭터는 현재 위치를 목표로 설정하여 애니메이션을 멈춤(Idle) 상태로 전환
                            follower.UpdateHardwarePosition(targetObj.transform.position);
                        }
                    }
                }
            }
        } catch (System.Exception e) { 
            Debug.LogError("JSON 파싱 또는 적용 중 에러 발생: " + e.Message);
        }
    }
}