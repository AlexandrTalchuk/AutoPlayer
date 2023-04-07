using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using MergeMarines.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum AutoReloadMode
{
    None,
    Simultaneously,
    Progressively,
}

public enum AutoSpawnMode
{
    None,
    Simultaneously,
    Progressively,
}

public enum AutoMergeMode
{
    None,
    SimultaneousFastest,
    SimultaneousByMergeLevel,
    SequentialByStars,
}

namespace MergeMarines._Application.Scripts.UI.GameWindow.Controls
{
    public class AutoPlayer : MonoBehaviour
    {
        private const float MergeAnimationDuration = 0.33f;
        private const float MergeAnimationHeight = 2f;
        private const float Delay = 0.5f;
        
        public static event Action<CurrencyType> AutoSpawnButtonClicked = delegate { };
        public static event Action AutoSpawnEnable = delegate { };
        public static event Action<BulletAttacker> AutoReloadEnable = delegate {  };

        [SerializeField]
        private Button _button;
        [SerializeField]
        private TextMeshProUGUI _spawnLabel;
        [SerializeField]
        private TextMeshProUGUI _costLabel;
        [SerializeField]
        private Image _currencyImage;
        [SerializeField]
        private TextMeshProUGUI _timerLabel;
        [SerializeField]
        private Slider _slider;
        [SerializeField]
        private GameObject _modeShape;
        
        private float _currentTime = 0f;
        private int _autoSpawnIteration = 0;
        private float _autoSpawnCooldown = 0f;
        private float _autoReloadCooldown = 0f;
        private bool _isAutoMode = false;
        private bool _isTimerEnable = false;

        public bool IsAutoMode => _isAutoMode;
        public int AutoSpawnIteration => _autoSpawnIteration;

        private List<int> _currentMerges = new();

        private void Start()
        {
            _button.onClick.AddListener(OnAutoModeClick);
        }

        private void OnEnable()
        {
            GameManager.GameStarted += GameManager_GameStarted;
        }

        private void OnDisable()
        {
            GameManager.GameStarted -= GameManager_GameStarted;
        }

        public void Setup()
        {
            _isAutoMode = false;
            _isTimerEnable = false;
            _autoSpawnCooldown = 0f;
            _autoSpawnIteration = 0;
            SetCurrentTime();
            _modeShape.gameObject.SetActive(!LocalConfig.IsBasicTutorialNeeded || GameManager.Instance.DungeonStage > 3);
        }

        public void ChoseButtonType(CurrencyType currencyType, AutoSpawnData autoSpawnData)
        {
            if (!_isAutoMode)
            {
                CalculateCost(autoSpawnData);
                _spawnLabel.text = _isAutoMode ? Strings.Manual : Strings.Auto;
                _timerLabel.text = $"{autoSpawnData.Time} {Strings.Seconds}";
                _slider.maxValue = autoSpawnData.Time;
                SetSliderValue();

                EnableTimerAndDisableModeType(true);

                if (currencyType != CurrencyType.None && !TutorialManager.IsTutorial)
                {
                    _currencyImage.gameObject.SetActive(true);
                    _currencyImage.sprite = IconManager.GetCurrencyIcon(currencyType);
                }
                else
                {
                    _currencyImage.gameObject.SetActive(false);
                }
            }
            else if (!_isTimerEnable)
            {
                _isTimerEnable = true;
                EnableTimerAndDisableModeType(false);
                StartCoroutine(AnimateTimer(autoSpawnData));
                _costLabel.text = $"{Strings.CommonOff}";
                _currencyImage.gameObject.SetActive(false);
            }
        }

        #region AutoLogic

        private void StartAutoMode() => StartCoroutine(AutoMode());

        private IEnumerator AutoMode()
        {
            while (_currentTime > 1)
            {
                AutoSpawn(LocalConfig.AutoSpawnMode);
                AutoMerge(LocalConfig.AutoMergeMode);
                AutoReload(LocalConfig.AutoReloadMode);
                
                yield return null;
            }
        }

        private void AutoSpawn(AutoSpawnMode spawnMode)
        {
            switch (spawnMode)
            {
                case AutoSpawnMode.None:
                    return;

                case AutoSpawnMode.Progressively:
                    ProgressivelyAutoSpawn();
                    break;

                case AutoSpawnMode.Simultaneously:
                    SimultaneouslyAutoSpawn();
                    break;
            }

            void ProgressivelyAutoSpawn()
            {
                if (_isAutoMode && _autoSpawnCooldown > BattleData.Data.AutoSpawnCooldown && TryPurchaseUnit())
                {
                    _autoSpawnCooldown = 0;
                    AutoSpawnEnable();
                }
                else
                {
                    _autoSpawnCooldown += Time.deltaTime;
                }
            }

            void SimultaneouslyAutoSpawn()
            {
                if (_isAutoMode && TryPurchaseUnit())
                    AutoSpawnEnable();
            }

            bool TryPurchaseUnit()
            {
                return UnitPlacer.Instance.TryPurchaseUnit(UnitPlacer.Instance.GetUnitForSpawn(),
                    UnitPlacer.Instance.CurrentPlaceCost);
            }
        }

        private void AutoReload(AutoReloadMode reloadMode)
        {
            foreach (var unit in UnitPlacer.Instance.AllUnits)
            {
                var bulletAttacker = unit.Attacker;

                if (bulletAttacker.IsReloading)
                    switch (reloadMode)
                    {
                        case AutoReloadMode.None:
                            break;

                        case AutoReloadMode.Progressively:
                            ReloadWithCooldown(bulletAttacker);
                            break;

                        case AutoReloadMode.Simultaneously:
                            Reload(bulletAttacker);
                            break;
                    }
            }

            void ReloadWithCooldown(BulletAttacker bulletAttacker)
            {
                if (_autoReloadCooldown > Delay)
                {
                    _autoReloadCooldown = 0;
                    Reload(bulletAttacker);
                }
                else
                {
                    _autoReloadCooldown += Time.deltaTime;
                }
            }
            
            void Reload(BulletAttacker bulletAttacker)
            {
                AutoReloadEnable(bulletAttacker);
                bulletAttacker.FastReload();
                AudioManager.Play3D(SoundType.TapRecharge, transform.position);
            }
        }

        private void AutoMerge(AutoMergeMode autoMergeMode)
        {
            if (autoMergeMode == AutoMergeMode.None)
                return;

            List<(Unit unit1, Unit unit2)> unitsToMerge = UnitPlacer.Instance.GetSortMergePairs();

            if (autoMergeMode == AutoMergeMode.SimultaneousFastest)
            {
                foreach ((Unit unit1, Unit unit2) in unitsToMerge)
                {
                    MergeAnimated(unit1, unit2);
                }
            }
            else if (autoMergeMode == AutoMergeMode.SimultaneousByMergeLevel)
            {
                foreach ((Unit unit1, Unit unit2) in unitsToMerge)
                {
                    if (!_currentMerges.Any(level => level < unit1.MergeLevel))
                        MergeAnimated(unit1, unit2);
                }
            }
            else if (autoMergeMode == AutoMergeMode.SequentialByStars)
            {
                if (_currentMerges.Count == 0)
                {
                    if (unitsToMerge.Count > 0)
                        MergeAnimated(unitsToMerge[0].unit1, unitsToMerge[0].unit2);
                }
            }
        }

        private void MergeAnimated(Unit unit1, Unit unit2) =>
            StartCoroutine(MergeAnimatedCoroutine(unit1, unit2));

        private IEnumerator MergeAnimatedCoroutine(Unit unit1, Unit unit2)
        {
            Vector3 sourcePosition = unit1.transform.position;
            Vector3 targetPosition = unit2.transform.position;

            unit1.InAutoMerge = true;
            unit2.InAutoMerge = true;
            
            _currentMerges.Add(unit1.MergeLevel);
            
            for (float elapsedDuration = 0; elapsedDuration < MergeAnimationDuration; elapsedDuration += Time.deltaTime)
            {
                float normalizedDuration = elapsedDuration / MergeAnimationDuration;

                Vector3 position = Vector3.Lerp(sourcePosition, targetPosition, normalizedDuration);

                float height = (-(((normalizedDuration - 0.5f) * 2) * ((normalizedDuration - 0.5f) * 2)) + 1)
                               * MergeAnimationHeight;

                position += Vector3.up * height;

                unit1.transform.position = position;

                yield return null;
            }
            
            unit1.InAutoMerge = false;
            unit2.InAutoMerge = false;
            
            _currentMerges.Remove(unit1.MergeLevel);

            UnitPlacer.Instance.Merge(unit1, unit2);
        }

        #endregion
        
        private void EnableTimerAndDisableModeType(bool state)
        {
            _spawnLabel.enabled = state;
            _slider.gameObject.SetActive(!state);
            _timerLabel.enabled = !state;
        }

        private void CalculateCost(AutoSpawnData autoSpawnData)
        {
            if (!TutorialManager.IsTutorial)
            {
                if (autoSpawnData.Cost > 0)
                    _costLabel.text = autoSpawnData.Cost.ToString();
                else if (autoSpawnData.Cost == 0)
                    _costLabel.text = Strings.FreeTitle;
            }
            else
            {
                _currencyImage.gameObject.SetActive(false);
                _costLabel.text = Strings.FreeTitle;
            }
        }

        private IEnumerator AnimateTimer(AutoSpawnData autoSpawnData)
        {
            while (_currentTime > 1)
            {
                _currentTime -= 0.1f;
                _slider.value -= 0.1f;
                _timerLabel.text = $"{(int)_currentTime} {Strings.Seconds}";
                yield return new WaitForSeconds(0.1f);
            }

            if (_currentTime <= 1)
            {
                _isAutoMode = false;
                _isTimerEnable = false;
                _currentTime = autoSpawnData.Time;
            }
        }

        private void SetSliderValue()
        {
            _slider.DOKill();
            _slider.value = _slider.maxValue;
        }

        private void SetCurrentTime()
        {
            _currentTime = BattleData.Data.AutoSpawnSettings[_autoSpawnIteration].Time;
        }

        private void OnAutoModeClick()
        {
            CurrencyType currencyType = BattleData.Data.AutoSpawnSettings[_autoSpawnIteration].CurrencyType;
            int cost = BattleData.Data.AutoSpawnSettings[_autoSpawnIteration].Cost;

            if (!_isAutoMode && !_isTimerEnable && !TutorialManager.IsTutorial)
            {
                _autoSpawnIteration++;
                _autoSpawnIteration = Math.Clamp(_autoSpawnIteration, 0, BattleData.Data.AutoSpawnSettings.Length - 1);

                if (currencyType is CurrencyType.Ads)
                {
                    UserManager.Instance.GetAutoSpawnByAds(isSuccess =>
                    {
                        UISystem.ShowWindow<PausePopup>();

                        if (isSuccess && !UISystem.Instance.IsSwitchingWindow)
                        {
                            UISystem.ReturnToPreviousWindow();
                            ChangeSpawnMode(currencyType);
                        }
                    });
                }
                else
                {
                    if (UserManager.Instance.TrySpendCurrency(currencyType, cost, "ItemType.AutoSpawn", "AutoSpawn", 1))
                    {
                        ChangeSpawnMode(currencyType);
                    }
                }
            }
            else if (!_isAutoMode && !_isTimerEnable && TutorialManager.IsTutorial)
            {
                _autoSpawnIteration = 0;
                ChangeSpawnMode(currencyType);
            }
            else if (_isAutoMode)
            {
                _isTimerEnable = false;

                ChangeSpawnMode(currencyType);
                StopAllCoroutines();
                SetCurrentTime();
                SetSliderValue();
            }
        }

        private void ChangeSpawnMode(CurrencyType currencyType)
        {
            _isAutoMode = !_isAutoMode;

            if (_isAutoMode)
            {
                _autoSpawnCooldown = 0;
            }

            StartAutoMode();

            AutoSpawnEnable();
            AutoSpawnButtonClicked(currencyType);
        }

        #region Events
        
        private void GameManager_GameStarted(DungeonType obj)
        {
            _autoSpawnIteration = 0;
        }
        
        #endregion
        
    }
}