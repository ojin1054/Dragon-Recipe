using System.Collections;
using DragonRecipe.Core;
using DragonRecipe.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DragonRecipe.Tests
{
    /// <summary>화면 조이스틱 크기·자리·고정/터치한 곳 설정</summary>
    public class JoystickSettingsTests
    {
        string _savePath;
        System.Action _restoreStick;

        [SetUp]
        public void SetUp()
        {
            _restoreStick = StickSettings.Snapshot();
            StickSettings.ResetAll();
            _savePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dragon_stick_{System.Guid.NewGuid():N}.json");
            DragonRecipe.Care.SaveSystem.PathOverride = _savePath;
            DragonRecipe.Care.DragonCare.UnloadForTests();
        }

        [TearDown]
        public void TearDown()
        {
            GamePause.Set(false);
            DragonRecipe.Care.DragonCare.UnloadForTests();
            DragonRecipe.Care.SaveSystem.PathOverride = null;
            if (System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
            _restoreStick?.Invoke();
        }

        [Test]
        public void Settings_AreClamped_AndReset()
        {
            StickSettings.Size = 5f;
            Assert.AreEqual(StickSettings.MaxSize, StickSettings.Size, 0.001f);
            StickSettings.Size = 0.1f;
            Assert.AreEqual(StickSettings.MinSize, StickSettings.Size, 0.001f);
            StickSettings.Position = new Vector2(-1f, 2f);
            Assert.IsTrue(StickSettings.HasPosition);
            Assert.AreEqual(new Vector2(0f, 1f), StickSettings.Position);
            StickSettings.Mode = StickMode.Floating;
            Assert.AreEqual(StickMode.Floating, StickSettings.Mode);

            StickSettings.ResetAll();
            Assert.IsFalse(StickSettings.HasPosition);
            Assert.AreEqual(1f, StickSettings.Size, 0.001f);
            Assert.AreEqual(StickMode.Fixed, StickSettings.Mode);
        }

        static Vector2 ScreenPoint(RectTransform parent, Vector2 local)
        {
            var canvas = parent.GetComponentInParent<Canvas>().rootCanvas;
            var cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.WorldToScreenPoint(cam, parent.TransformPoint(local));
        }

        [UnityTest]
        public IEnumerator Stick_SizePosition_AndTouchToPlace()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;
            yield return null;

            var layout = JoystickLayout.Active;
            Assert.IsNotNull(layout, "화면 조이스틱에 설정이 붙지 않음");
            var area = layout.Area;
            var parent = (RectTransform)area.parent;
            Vector2 baseSize = area.sizeDelta;
            float baseRange = layout.MovementRange;
            Vector2 defaultCenter = layout.Center;

            // 크기
            StickSettings.Size = 1.5f;
            Assert.AreEqual(baseSize.x * 1.5f, area.sizeDelta.x, 0.5f, "크기 설정이 조이스틱에 반영되지 않음");
            Assert.AreEqual(baseRange * 1.5f, layout.MovementRange, 0.5f, "커지면 움직이는 범위도 커져야 함");

            // 자리
            StickSettings.Position = new Vector2(0.3f, 0.45f);
            var r = parent.rect;
            var expected = r.min + Vector2.Scale(new Vector2(0.3f, 0.45f), r.size);
            Assert.Less(Vector2.Distance(expected, layout.Center), 1f, "자리 설정이 반영되지 않음");
            Assert.Greater(Vector2.Distance(defaultCenter, layout.Center), 10f);

            // 고정 모드: 터치 판 없음
            Assert.IsFalse(layout.Zone.gameObject.activeSelf, "고정 모드에서는 화면 아무 데나 눌러도 옮겨 오지 않음");

            // 터치한 곳 모드: 누른 곳으로 옮겨 오고, 떼면 제자리
            StickSettings.Mode = StickMode.Floating;
            Assert.IsTrue(layout.Zone.gameObject.activeSelf, "터치한 곳 모드인데 터치 판이 없음");
            Vector2 home = layout.Center;
            Vector2 touch = home + new Vector2(120f, -60f);
            var e = new PointerEventData(EventSystem.current) { position = ScreenPoint(parent, touch), pointerId = 3 };
            layout.Zone.OnPointerDown(e);
            Assert.IsTrue(layout.IsHeld);
            Assert.Less(Vector2.Distance(touch, layout.Center), 1f, "누른 곳으로 조이스틱이 옮겨 오지 않음");

            e.position += new Vector2(60f, 0f);
            layout.Zone.OnDrag(e);
            Assert.Greater(layout.Knob.anchoredPosition.x, 1f, "끌어도 손잡이가 움직이지 않음");

            layout.Zone.OnPointerUp(e);
            Assert.IsFalse(layout.IsHeld);
            Assert.Less(Vector2.Distance(home, layout.Center), 1f, "손을 떼면 제자리로 돌아가야 함");
            Assert.AreEqual(Vector2.zero, layout.Knob.anchoredPosition);
        }

        [UnityTest]
        public IEnumerator PauseMenu_OpensStickSettings()
        {
            yield return SceneManager.LoadSceneAsync("FlightPrototype");
            yield return null;

            var menu = Object.FindFirstObjectByType<PauseMenu>();
            Assert.IsNotNull(menu.stickButton, "조이스틱 설정 버튼이 없음");
            Assert.IsNotNull(menu.stickPanel);
            var layout = JoystickLayout.Active;

            GamePause.Set(true);
            menu.stickButton.onClick.Invoke();
            var panel = menu.stickPanel;
            Assert.IsTrue(panel.IsOpen, "조이스틱 설정이 안 열림");

            panel.biggerButton.onClick.Invoke();
            Assert.AreEqual(1.1f, StickSettings.Size, 0.001f);
            panel.modeButton.onClick.Invoke();
            Assert.AreEqual(StickMode.Floating, StickSettings.Mode);

            panel.moveButton.onClick.Invoke();
            Assert.IsTrue(panel.IsMoving);
            Assert.IsTrue(layout.IsEditing);
            var parent = (RectTransform)layout.Area.parent;
            var target = parent.rect.center;
            var overlay = panel.editOverlay.GetComponent<StickEditOverlay>();
            overlay.OnDrag(new PointerEventData(EventSystem.current) { position = ScreenPoint(parent, target) });
            Assert.IsTrue(StickSettings.HasPosition, "옮긴 자리가 저장되지 않음");
            Assert.Less(Vector2.Distance(target, layout.Center), 1f);
            panel.doneButton.onClick.Invoke();
            Assert.IsFalse(panel.IsMoving);
            Assert.IsFalse(layout.IsEditing);

            panel.resetButton.onClick.Invoke();
            Assert.IsFalse(StickSettings.HasPosition);
            Assert.AreEqual(StickMode.Fixed, StickSettings.Mode);

            GamePause.Set(false);
            Assert.IsFalse(panel.IsOpen, "메뉴를 닫으면 설정도 닫혀야 함");
        }
    }
}
