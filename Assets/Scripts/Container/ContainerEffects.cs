using DG.Tweening;
using MultiSuika.Cannon;
using MultiSuika.Manager;
using UnityEngine;
using UnityEngine.Serialization;

namespace MultiSuika.Container
{
    public class ContainerEffects : MonoBehaviour
    {
        [Header("VFXs")] 
        [SerializeField] private ParticleSystem _speedLines;
        [SerializeField] private SpriteRenderer _winOutsideSprite;
        [SerializeField] private ParticleSystem _loseExplosion;
        [SerializeField] private ParticleSystem _glowEffect;
        [SerializeField] private ParticleSystem _thrusterParticleSystem;
        [SerializeField] private ParticleSystem _thrusterParticleSystemWin;

        [Header("Lead Parameters")]
        [SerializeField] private float _speedLinesMinScale; // 3
        [SerializeField] private float _speedLinesMaxScale; // 5
        [FormerlySerializedAs("_speedLinesMinRateOverEmission")] [SerializeField] private float _speedLinesMinRateOverTime; // 100
        [SerializeField] private float _speedLinesMaxRateOverEmission; // 250
        
        [SerializeField] private float _glowDuration;
        
        [Header("Win Parameters")] 
        [SerializeField] private SpriteRenderer _containerBackgroundSkin;
        [SerializeField] private ContainerCameraMovements _containerCameraMovements;
        
        [Header("Movement burst")]
        [SerializeField] private float _hitDuration = 0.5f;
        [SerializeField] private Vector3 _hitStrength;
        [SerializeField] private int _hitVibrato = 20;
        [SerializeField] private float _hitRandomness = 90f;
        [SerializeField] private bool _hitFadeOut = true;
        [SerializeField] private ShakeRandomnessMode _hitMode = ShakeRandomnessMode.Full;
        [SerializeField] private AnimationCurve _easeCurve;
        
        [Header("Thruster effect")]
        [SerializeField] private float _rateOverTime;
        [SerializeField] private Gradient _colorGradient;
        [SerializeField] private ParticleSystem.MinMaxCurve _sizeOverTime;


        private int _playerIndex;
        private Sequence _speedLinesSequence;

        private void Awake()
        {
            _speedLinesSequence = DOTween.Sequence();
        }

        private void Start()
        {
            _playerIndex = ContainerTracker.Instance.GetPlayerFromItem(GetComponentInParent<ContainerInstance>());
            
            var effectAssets = VersusManager.Instance.GetContainerEffectAssets(_playerIndex);
            _winOutsideSprite.sprite = effectAssets.outside;
            _glowEffect.GetComponent<Renderer>().material = effectAssets.glow;
            
            VersusManager.Instance.OnLeadStart.Subscribe(OnLeadStart, _playerIndex);
            VersusManager.Instance.OnLeadStop.Subscribe(OnLeadStop, _playerIndex);
            VersusManager.Instance.OnGameOver.Subscribe(OnGameOver, _playerIndex);
        }

        #region GameOver

        private void OnGameOver(bool hasWon)
        {
            if (_speedLinesSequence.IsPlaying())
            {
                _speedLinesSequence.Kill();
            }
            _speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _glowEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (hasWon)
            {
                OnWin();
            }
            else
            {
                OnLose();
            }
        }

        private void OnWin()
        {
            var nextBallSpriteRenderer = CannonTracker.Instance.GetItemFromPlayerOrDefault(_playerIndex)
                .GetNextBallSpriteRenderer();
            var cannonSpriteRenderer = CannonTracker.Instance.GetItemFromPlayerOrDefault(_playerIndex).spriteRenderer;
            var thrusterVfxMain = _thrusterParticleSystem.main;
            var cameraJoints = _containerCameraMovements.GetCameraJointsTransform();
            
            var winSequence = DOTween.Sequence();
            winSequence
                .Append(_winOutsideSprite.DOFade(1, 0.8f))
                .Join(_containerBackgroundSkin.DOFade(0, 0.8f).SetEase(Ease.InQuart))
                .Join(nextBallSpriteRenderer.DOFade(0, 0.8f).SetEase(Ease.InQuart))
                .Join(cannonSpriteRenderer.DOFade(0, 0.8f).SetEase(Ease.InQuart))
                .AppendCallback(() => thrusterVfxMain.loop = false)
                .AppendInterval(0.6f)
                .AppendCallback(() => _thrusterParticleSystemWin.Play())
                .AppendInterval(0.5f)
                .Append(cameraJoints.secondaryTf.DOShakePosition(_hitDuration, _hitStrength, _hitVibrato,
                    _hitRandomness,
                    fadeOut: _hitFadeOut, randomnessMode: _hitMode).SetLoops(10000))
                .AppendInterval(0.8f)
                .Insert(2f, cameraJoints.mainTf.DOMoveY(-30, 2.5f).SetEase(_easeCurve));
        }

        // private void SwitchToWinThruster()
        // {
        //     // var main = _thrusterParticleSystem.main;
        //     // var emission = _thrusterParticleSystem.emission;
        //     // var colorOverLifetime = _thrusterParticleSystem.colorOverLifetime;
        //     // var sizeOverLifetime = _thrusterParticleSystem.sizeOverLifetime;
        //     //
        //     // main.loop = true;
        //     // emission.rateOverTime = 15;
        //     // colorOverLifetime.color = _colorGradient;
        //     // sizeOverLifetime.size = _sizeOverTime;
        //     // var thruster = _thrusterParticleSystem.main;
        //     // thruster.loop = false;
        //     _thrusterParticleSystemWin.Play();
        // }

        private void OnLose()
        {
            _loseExplosion.Play();
        }

        #endregion

        #region Lead

        private void OnLeadStart(float timerDuration)
        {
            if (_speedLinesSequence.IsPlaying())
            {
                _speedLinesSequence.Kill();
            }
            _speedLinesSequence = DOTween.Sequence();
            
            // For now, let's say that the minimum timer duration will safeguard us for any issue there
            var speedLinesRampUpDuration = Mathf.Max(timerDuration - _glowDuration, 1);

            var speedLineEmission = _speedLines.emission;
            
            _speedLines.transform.localScale = Vector3.one * _speedLinesMinScale;
            speedLineEmission.rateOverTime = _speedLinesMinRateOverTime;
            
            _speedLinesSequence.AppendCallback(() => _speedLines.Play())
                .Append(_speedLines.transform.DOScale(_speedLinesMaxScale, speedLinesRampUpDuration))
                .Join(DOTween.To(() => speedLineEmission.rateOverTime.constant,
                    x => speedLineEmission.rateOverTime = x, _speedLinesMaxRateOverEmission,
                    speedLinesRampUpDuration))
                .AppendCallback(() => _glowEffect.Play());
        }

        private void OnLeadStop(bool x)
        {
            if (_speedLinesSequence.IsPlaying())
            {
                _speedLinesSequence.Kill();
            }
            _speedLines.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _glowEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        #endregion
    }
}