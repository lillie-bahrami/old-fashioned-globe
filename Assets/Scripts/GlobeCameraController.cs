using Esri.ArcGISMapsSDK.Utils.GeoCoord;
using Esri.ArcGISMapsSDK.Utils.Math;
using Esri.HPFramework;
using System;
using UnityEngine;
using Esri.ArcGISMapsSDK.Components;


/// <summary>
/// Copied from ArcGISCameraControllerComponent, camera only allowed updates to longitude
/// </summary>

[DisallowMultipleComponent]
[RequireComponent(typeof(HPTransform))]

public class GlobeCameraController : MonoBehaviour
{
	private ArcGISMapViewComponent arcGISMapViewComponent;
	private HPTransform hpTransform;

	private float TranslationSpeed = 0.0f;
	private double MouseScrollSpeed = 0.1f;

	private static double MaxCameraHeight = 11000000.0;
	private static double MinCameraHeight = 1.8;

	private LatLon lastLatLon = new LatLon();
	private double lastDotVC = 0.0f;
	private bool firstDragStep = true;

	private Vector3 lastMouseScreenPosition;
	private bool firstOnFocus = true;

	public double MaxSpeed = 2000000.0;
	public double MinSpeed = 1000.0;

	private void Awake()
	{
		lastMouseScreenPosition = Input.mousePosition;

		Application.focusChanged += FocusChanged;
	}

	void OnEnable()
	{
		arcGISMapViewComponent = gameObject.GetComponentInParent<ArcGISMapViewComponent>();
		hpTransform = GetComponent<HPTransform>();
	}

	void Start()
	{
		if (arcGISMapViewComponent == null)
		{
			Debug.LogError("An ArcGISMapViewComponent could not be found. Please make sure this GameObject is a child of a GameObject with an ArcGISMapViewComponent attached");

			enabled = false;
			return;
		}

	}

	void Update()
	{
		if (arcGISMapViewComponent == null)
		{
			return;
		}

		var cartesianPosition = new Vector3d(hpTransform.DUniversePosition.x, hpTransform.DUniversePosition.y, hpTransform.DUniversePosition.z);
		var cartesianRotation = hpTransform.UniverseRotation;

		var latLon = arcGISMapViewComponent.Scene.FromCartesianPosition(cartesianPosition);

		DragMouseEvent(ref cartesianPosition, ref cartesianRotation);

		var oldENUReference = arcGISMapViewComponent.Scene.GetENUReference(cartesianPosition).ToMatrix4x4();

		var altitude = latLon.Altitude;

		UpdateSpeed(altitude); //camera moves faster @ higher alt

		var forward = new Vector3d(hpTransform.Forward.x, hpTransform.Forward.y, hpTransform.Forward.z);
		var right = new Vector3d(hpTransform.Right.x, hpTransform.Right.y, hpTransform.Right.z);

		var movDir = Vector3d.zero;

		bool changed = false;

		if (Input.GetAxis("Horizontal") != 0)
		{
			movDir += right * Input.GetAxis("Horizontal") * TranslationSpeed * Time.deltaTime;
			changed = true;
		}

		if (Input.mouseScrollDelta.y != 0.0)
		{
			var delta = System.Math.Max(1.0, (altitude - MinCameraHeight)) * MouseScrollSpeed * Input.mouseScrollDelta.y;
			movDir += forward * delta;
			changed = true;
		}

		if (changed)
		{
			var distance = movDir.Length();
			movDir /= distance;

			if (arcGISMapViewComponent.ViewMode == Esri.GameEngine.Map.ArcGISMapType.Global)
			{
				var nextLatLon = GeoUtils.UnityCartesianToWGS84LatLon(movDir + cartesianPosition);

				if (nextLatLon.Altitude > MaxCameraHeight)
				{
					Geometry.RayWGS84EllipsoidIntersection(cartesianPosition, -movDir, MaxCameraHeight, out var intersection);
					cartesianPosition -= movDir * intersection;
				}
				else if (nextLatLon.Altitude < MinCameraHeight)
				{
					Geometry.RayWGS84EllipsoidIntersection(cartesianPosition, movDir, MinCameraHeight, out var intersection);
					cartesianPosition += movDir * intersection;
				}
				else
				{
					cartesianPosition += movDir * distance;
				}

				var newENUReference = arcGISMapViewComponent.Scene.GetENUReference(cartesianPosition).ToMatrix4x4();

				cartesianRotation = Quaternion.Inverse(oldENUReference.ToQuaternion()) * cartesianRotation;
				cartesianRotation = newENUReference.ToQuaternion() * cartesianRotation;
			}
			else
			{
				cartesianPosition += movDir * distance;
			}
		}

		hpTransform.DUniversePosition = new DVector3(cartesianPosition.x, cartesianPosition.y, cartesianPosition.z);
		hpTransform.UniverseRotation = cartesianRotation;
	}

	void OnTransformParentChanged()
	{
		OnEnable();
	}

	public void SetupMaxMinSpeed(double max, double min)
	{
		MaxSpeed = max;
		MinSpeed = min;
	}

	private void DragMouseEvent(ref Vector3d cartesianPosition, ref Quaternion cartesianRotation)
	{
		var deltaMouse = Input.mousePosition - lastMouseScreenPosition;

		if (!firstOnFocus)
		{
			if (Input.GetMouseButton(0))
			{
				if (deltaMouse != Vector3.zero)
				{
					GlobalDragging(ref cartesianPosition, ref cartesianRotation);
				}
			}
			else if (!Input.GetMouseButton(0))
			{
				firstDragStep = true;
			}
		}
		else
		{
			firstOnFocus = false;
		}

		lastMouseScreenPosition = Input.mousePosition;
	}

	void GlobalDragging(ref Vector3d cartesianPosition, ref Quaternion cartesianRotation)
	{
		var worldRayDir = GetMouseRayCastDirection();
		var isIntersected = Geometry.RayWGS84EllipsoidIntersection(cartesianPosition, worldRayDir, 0, out var intersection);

		if (isIntersected && intersection >= 0)
		{
			var oldENUReference = arcGISMapViewComponent.Scene.GetENUReference(cartesianPosition).ToMatrix4x4();

			var latLon = arcGISMapViewComponent.Scene.FromCartesianPosition(cartesianPosition);

			Vector3d cartesianCoord = cartesianPosition + worldRayDir * intersection;
			var currentLatLon = GeoUtils.UnityCartesianToWGS84LatLon(cartesianCoord);

			var visibleHemisphereDir = Vector3d.Normalize(GeoUtils.WGS84LatLonToUnityCartesian(new LatLon(0, latLon.Longitude)));

			double dotVC = Vector3d.Dot(cartesianCoord, visibleHemisphereDir);
			lastDotVC = firstDragStep ? dotVC : lastDotVC;

			double deltaLongitude = firstDragStep ? 0 : lastLatLon.Longitude - currentLatLon.Longitude;

			lastLatLon.Longitude = currentLatLon.Longitude + deltaLongitude;

			latLon.Longitude = latLon.Longitude + deltaLongitude;

			cartesianPosition = arcGISMapViewComponent.Scene.ToCartesianPosition(latLon);

			var newENUReference = arcGISMapViewComponent.Scene.GetENUReference(cartesianPosition).ToMatrix4x4();
			cartesianRotation = Quaternion.Inverse(oldENUReference.ToQuaternion()) * cartesianRotation;
			cartesianRotation = newENUReference.ToQuaternion() * cartesianRotation;

			firstDragStep = false;
			lastDotVC = dotVC;
		}
	}

	Vector3d GetMouseRayCastDirection()
	{
		var forward = hpTransform.Forward;
		var right = hpTransform.Right;
		var up = hpTransform.Up;

		var camera = gameObject.GetComponent<Camera>();

		Matrix4x4d view;
		view.m00 = right.x; view.m01 = up.x; view.m02 = forward.x; view.m03 = 0;
		view.m10 = right.y; view.m11 = up.y; view.m12 = forward.y; view.m13 = 0;
		view.m20 = right.z; view.m21 = up.z; view.m22 = forward.z; view.m23 = 0;
		view.m30 = 0; view.m31 = 0; view.m32 = 0; view.m33 = 1;

		Matrix4x4d proj;
		Matrix4x4 inverseProj = camera.projectionMatrix.inverse;
		proj.m00 = inverseProj.m00; proj.m01 = inverseProj.m01; proj.m02 = inverseProj.m02; proj.m03 = inverseProj.m03;
		proj.m10 = inverseProj.m10; proj.m11 = inverseProj.m11; proj.m12 = inverseProj.m12; proj.m13 = inverseProj.m13;
		proj.m20 = inverseProj.m20; proj.m21 = inverseProj.m21; proj.m22 = inverseProj.m22; proj.m23 = -inverseProj.m23;
		proj.m30 = inverseProj.m30; proj.m31 = inverseProj.m31; proj.m32 = -inverseProj.m32; proj.m33 = inverseProj.m33;

		Vector3d ndcCoord = new Vector3d(2.0 * (Input.mousePosition.x / Screen.width) - 1.0, 2.0 * (Input.mousePosition.y / Screen.height) - 1.0, 1);
		Vector3d viewRayDir = Vector3d.Normalize(proj.MultiplyPoint(ndcCoord));
		return view.MultiplyVector(viewRayDir);
	}

	void FocusChanged(bool isFocus)
	{
		firstOnFocus = true;
	}

	void UpdateSpeed(double height)
	{
		var msMaxSpeed = (MaxSpeed * 1000) / 3600;
		var msMinSpeed = (MinSpeed * 1000) / 3600;
		TranslationSpeed = (float)(Math.Pow(Math.Min((height / 100000.0), 1), 2.0) * (msMaxSpeed - msMinSpeed) + msMinSpeed);
	}

}
