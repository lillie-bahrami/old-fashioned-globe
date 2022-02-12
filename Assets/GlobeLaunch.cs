using System.Collections;

using Esri.HPFramework;
using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.ArcGISMapsSDK.Components;
using Esri.GameEngine.Map;
using Esri.Unity;

using UnityEngine;

public class GlobeLaunch : MonoBehaviour
{
	public float Longitude;
	public float Latitude;
	public float Altitude;

	private string renderContainerName = "AGSContainer";

	public GameObject armPrefab;

	private GameObject ARM;

	void Start()
	{
		BuildGlobe();

        StartCoroutine(Parenting());
    }

	void BuildGlobe()
	{
		//Map View setup
		var viewMode = ArcGISMapType.Global;
		var arcGISMap = new Esri.GameEngine.Map.ArcGISMap(viewMode);
		var arcGISMapViewComponent = gameObject.AddComponent<ArcGISMapViewComponent>();
		arcGISMapViewComponent.Position = new LatLon(Latitude, Longitude, Altitude);
		arcGISMapViewComponent.ViewMode = viewMode;

		//Not assigning a basemap, instead adding DarkGrayCanvas as the first layer
		var base_layer = new Esri.GameEngine.Layers.ArcGISImageLayer("https://services.arcgisonline.com/arcgis/rest/services/Canvas/World_Dark_Gray_Base/MapServer", "base", 1.0f, true, "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b");
		arcGISMap.Layers.Add(base_layer);

		/*
		 * // Reference Layer
		 * var ref_layer = new Esri.GameEngine.Layers.ArcGISImageLayer("https://services.arcgisonline.com/arcgis/rest/services/Canvas/World_Dark_Gray_Reference/MapServer", "reference", 1.0f, true, "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b");
		 * arcGISMap.Layers.Add(ref_layer);
		 */

		// Camera Setup
		var cameraGameObject = Camera.main.gameObject;
		cameraGameObject.AddComponent<ArcGISCameraComponent>();
		cameraGameObject.AddComponent<GlobeCameraController>(); //copy of normal controller, w/ latitude locked out

		var locationComponent = cameraGameObject.AddComponent<ArcGISLocationComponent>();
		locationComponent.Position = new LatLon(30, Longitude, Altitude);
		locationComponent.Rotation = new Rotator(15, 0, 0);

		// No dice
		//GameObject arm = Instantiate(armPrefab, cameraGameObject.transform);

		// Arm Setup
		GameObject arm = Instantiate(armPrefab);
		var armLocation = arm.AddComponent<ArcGISLocationComponent>();
		armLocation.Position = new LatLon(90, Longitude, 1000); //Arm longitude must follow camera longitude consistently
		armLocation.Rotation = new Rotator(0, 0, Longitude - 50f);
		armLocation.Scale = 1000000f;

		//Renderer setup
		var rendererGameObject = new GameObject(renderContainerName);
		rendererGameObject.AddComponent<ArcGISRendererComponent>();

		cameraGameObject.transform.SetParent(arcGISMapViewComponent.transform, false);
		rendererGameObject.transform.SetParent(arcGISMapViewComponent.transform, false);
		// 0 dice
		//arm.transform.SetParent(cameraGameObject.transform, false);
		//arm.transform.SetParent(cameraGameObject.GetComponent<HPTransform>().transform, false);
		arm.transform.SetParent(arcGISMapViewComponent.transform, false); //False works best

		ARM = arm;

		arcGISMapViewComponent.RendererView.Map = arcGISMap;
	}

	IEnumerator Parenting()
	{
		yield return new WaitForSeconds(1); //wait for HP Transform to set position, scale of arm
		ARM.GetComponent<HPTransform>().enabled = false;
		ARM.transform.parent = null;
		//These do not work:
		//ARM.transform.SetParent(Camera.main.gameObject.transform, false);
		//ARM.transform.SetParent(Camera.main.gameObject.transform, true);
		//ARM.transform.parent = Camera.main.gameObject.transform;
		ARM.transform.parent = Camera.main.gameObject.GetComponent<HPTransform>().transform; //this works!
	}
}
