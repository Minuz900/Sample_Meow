namespace MeowMeow
{
    public class NormalCustomerController : CustomerController, IPointerClickHandler
    {

        public enum NormalCustomerStateEnum
        {
            Waiting,            // 대기상태.
            Go_SalesStand,      // 판매대.
            PickupFood,         // 판매대 앞에서 음식 픽업.
            PickupAllComplete,  // 모든 음식 픽업 완료.
            Go_Counter,         // 계산대.
            Payment,            // 지불.
            Go_Out,             // 나가기.
        }

        public enum PickupStateEnum
        {
            NotPickup,
            Pickup
        }

        public enum CollisionStateEnum
        {
            Inside,
            Outside,
        }

        [Serializable]
        public class PickupSpace
        {
            public GameObject obj;
            public PickupStateEnum pickupState;
            public SpriteRenderer foodRender;
            public FoodInfoData foodInfo;

            public void PickupFood(FoodInfoData foodInfoData)
            {
                foodRender.sprite = foodInfoData.sprite;
                pickupState = PickupStateEnum.Pickup;
                foodInfo = foodInfoData;
                obj.SetActive(true);
            }

            public void SaleFood()
            {
                foodRender.sprite = null;
                pickupState = PickupStateEnum.NotPickup;
                obj.SetActive(false);
            }
        }

        #region Public Variables

        // 손님 정보.
        [Header("Data")]
        public NormalCustomerInfoData normalCustomerInfoData;

        // 손님 상태.
        [Header("State")]
        public NormalCustomerStateEnum normalCustomerState = NormalCustomerStateEnum.Waiting;

        // 현재 이동, 사용중인 장소.
        [Header("Place")]
        public SalesStandController salesStand;
        public CounterController counter;

        // 현재 가져와야, 가져온 음식 정보.
        [Header("Food Data")]
        public FoodInfoData foodData;

        // 음식 픽업 상태.
        [Header("Food Pickup")]
        public PickupStateEnum foodPickupState = PickupStateEnum.NotPickup;

        // 가게 In, Out 위치 상태.
        [Header("collisionState")]
        public CollisionStateEnum collisionStateEnum = CollisionStateEnum.Outside;

        // MeshAgent Start delegate.
        public NavMeshAgentCallback meshAgentStartCallback;

        // MeshAgent Complete delegate.
        public NavMeshAgentCallback meshAgentCompleteCallback;

        // 손님 더블 클릭시 delegate
        public GamePlayManager.CatDoubleClickCallback catDoubleClickCallback;

        // 사야하는 물품 리스트.
        public List<BuyFoodInfo> buyFoodInfos = new List<BuyFoodInfo>();

        #endregion


        #region Private Variables

        // 픽업 공간.
        [SerializeField]
        private List<PickupSpace> pickupSpaces;

        [Header("UI View")]

        // UI 관련 View 스크립트.
        [SerializeField]
        private NormalCustomerView normalCustomerView;

        // 마지막 클릭한 시간.
        private float lastClickTime;

        // 기다리는 공간 인덱스.
        private int waitingIndex;

        // 생성된 타입
        private GenerateType generateType = GenerateType.Normal;

        // UniTask Cancel Token.
        private CancellationTokenSource cancel = new CancellationTokenSource();

        #endregion


        #region Properties

        public int WaitingIndex
        {
            get
            {
                return this.waitingIndex;
            }
        }

        public GenerateType GenerateType
        {
            get
            {
                return this.generateType;
            }
        }

        #endregion


        #region MonoBehaviour Methods


        protected override void Start()
        {
            base.Start();

            // delegate 선언.
            this.meshAgentStartCallback = this.OnMeshAgentStartCallback;
            this.meshAgentCompleteCallback = this.OnMeshAgentCompleteCallback;

            // 픽업 공간 초기화.
            this.PickupSpaceInitialize();

            // 이동속도 초기화.
            this.RefreshMovementSpeed();

            // 울음소리 UniTask 등록.
            this.MeowWait().Forget();

            // 초기화 완료.
            this.InitComplete();
        }


        protected override void Update()
        {
            base.Update();

            if (!base.IsInit)
                return;

            if (this.normalCustomerState == NormalCustomerStateEnum.Waiting)
            {
                this.StartWorking();
                this.StartAgent();

            }
            else if (this.normalCustomerState == NormalCustomerStateEnum.PickupFood && this.salesStand != null)
            {
                if (this.CheckFoodCount() && this.CheckMyTurn())
                {
                    this.CheckState();
                    this.StartAgent();
                }
            }
        }


        #endregion


        #region Public Methods

        // 일 시작.
        public override void StartWorking()
        {
            base.StartWorking();
            this.InitState();
            this.NextStep();
        }

        // Customer 데이터 셋팅.
        public void SetNormalCustomerData(NormalCustomerInfoData data)
        {
            this.normalCustomerInfoData = data;
            this.InitSkin(data.skeletonDataAsset);
        }

        // 픽업 공간 초기화.
        public void PickupSpaceInitialize()
        {
            for (int i = 0; i < this.pickupSpaces.Count; i++)
            {
                this.pickupSpaces[i].pickupState = PickupStateEnum.NotPickup;
            }
        }

        // 빈 픽업 공간 가져오기. 
        public PickupSpace GetEmptyPickupSpace()
        {
            for (int i = 0; i < this.pickupSpaces.Count; i++)
            {
                var space = this.pickupSpaces[i];

                if (space.pickupState == PickupStateEnum.NotPickup)
                {
                    return space;
                }
            }
            return null;
        }

        // 이동속도 Refresh.
        public void RefreshMovementSpeed()
        {
            // 이동속도 Refresh 처리 코드 부분.
        }

        public void SetWaitingIndex(int index)
        {
            this.waitingIndex = index;
        }

        public void SetGenerateType(GenerateType type)
        {
            this.generateType = type;
        }


        // 사야하는 물품을 셋팅해준다.
        public void SetBuyFoodList(List<BuyFoodInfo> buyFoodInfos)
        {
            this.buyFoodInfos = new List<BuyFoodInfo>();
            this.buyFoodInfos = buyFoodInfos;
        }

        // 진입했을때 호출.
        public void Entrance(CollisionStateEnum StateEnum)
        {
            this.collisionStateEnum = StateEnum;
        }

        // 나갔을때 호출.
        public void Exit(CollisionStateEnum StateEnum)
        {
            this.collisionStateEnum = StateEnum;
            GamePlayManager.Instance.DeleteNormalCustomer(this, generateType);
        }

        public void CancelToken()
        {
            if (!cancel.IsCancellationRequested)
            {
                cancel.Cancel();
            }
        }

        // 더블클릭 delegate 설정. 
        public void SetDoubleClickCallback(GamePlayManager.CatDoubleClickCallback callback)
        {
            
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            
        }

        #endregion


        #region Private Methods

        // Agent 콜백 설정 및 상태 초기화.
        private void InitState()
        {
            base.Agent
                .OnPathStart(() => meshAgentStartCallback())
                .OnPathComplete(() => meshAgentCompleteCallback());

            this.normalCustomerState = NormalCustomerStateEnum.Waiting;
        }

        // 울음 소리 랜덤 재생.
        async UniTaskVoid MeowWait()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(UnityEngine.Random.Range(GameConstant.MinMeowTime, GameConstant.MaxMeowTime)), cancellationToken: cancel.Token);

            SoundManager.Instance.PlaySfx(SFXTypeEnum.CatMeow);
        }

        // Agent 시작.
        private void StartAgent()
        {
            base.Agent.Agent.isStopped = false;
        }

        // 다음 스텝 체크.
        private void NextStep()
        {
            this.normalCustomerState++;
            this.CheckState();
        }

        // 판매대로 이동 명령.
        private void GoSalesStand()
        {
            /* 판매대로 이동하는 코드 부분. */
        }

        // 판매대에서 음식 픽업.
        private void PickupFood()
        {
            if (this.CheckFoodCount() && this.CheckMyTurn())
            {
                this.salesStand.PickupFood(this.buyFoodInfos[0].count);

                GamePlayManager.Instance.inventoryFoodInfos[this.buyFoodInfos[0].foodInfoData] -= this.buyFoodInfos[0].count;
                GamePlayManager.Instance.DeleteCustomerFoodInfos(this.buyFoodInfos[0].foodInfoData, 1);

                for (int i = 0; i < this.buyFoodInfos[0].count; i++)
                {
                    var space = GetEmptyPickupSpace();
                    space.PickupFood(this.buyFoodInfos[0].foodInfoData);
                }

                this.buyFoodInfos.RemoveAt(0);

                if (this.buyFoodInfos.Count > 0)
                {
                    normalCustomerState = NormalCustomerStateEnum.Go_SalesStand;
                }
                else
                {
                    normalCustomerState++;

                    this.normalCustomerView.RefreshBuyAnim();
                }

                this.salesStand.DeleteNormalCustomer(this);

                this.CheckState();
            }
        }

        // 카운터 이동 명령.
        private void GoCounter()
        {
            this.counter = GamePlayManager.Instance.counterManager.FindEmptyCounter();
            base.Agent.SetDestination(this.counter.GetWatingPosition());
        }

        // 지불(계산).
        private async void Payment()
        {
            /* 계산 처리 코드 부분 */
        }


        private void CheckTip(CounterController counter, double resultGold)
        {
            /* 팁 처리 코드 부분. */
        }

        // 건물 밖으로 이동.
        private void GoOut()
        {
            var position = GamePlayManager.Instance.CustomerRandomPosition();
            base.Agent.SetDestination(position);
        }

        // 상태 체크.
        private void CheckState()
        {
            switch (normalCustomerState)
            {
                case NormalCustomerStateEnum.Go_SalesStand:

                    this.GoSalesStand();

                    break;

                case NormalCustomerStateEnum.PickupFood:

                    this.PickupFood();

                    break;

                case NormalCustomerStateEnum.PickupAllComplete:

                    this.NextStep();

                    break;

                case NormalCustomerStateEnum.Go_Counter:

                    this.GoCounter();
                    this.CheckTutorial();

                    break;

                case NormalCustomerStateEnum.Payment:

                    this.Payment();

                    break;

                case NormalCustomerStateEnum.Go_Out:

                    this.GoOut();

                    break;
            }
        }


        private void CheckTutorial()
        {
            /* 튜토리얼 체크 코드 부분. */
        }


        // 판매대에 사야 할 음식이 있는지 체크.
        private bool CheckFoodCount()
        {
            if (this.salesStand.currentFoodCount >= this.buyFoodInfos[0].count)
            {
                return true;
            }

            return false;
        }

        // 음식을 가져가야 할 순서인지 체크.
        private bool CheckMyTurn()
        {
            if (this.salesStand.watingCustomers[0] == this)
            {
                return true;
            }

            return false;
        }


        // 해당 음식이 진열되어있는 판매대를 찾는다.
        private SalesStandController FindSalesStand(FoodInfoData foodData)
        {
            var salesStand = GamePlayManager.Instance.salesStandManager.FindSalesStandByFood(foodData);
            return salesStand;
        }



        private void OnMeshAgentStartCallback()
        {
            /* Agent 시작 처리 부분 */
        }

        private void OnMeshAgentCompleteCallback()
        {
            /* Agent 완료 처리 부분 */

            switch (normalCustomerState)
            {
                case NormalCustomerStateEnum.Go_SalesStand:
				
                    this.salesStand.AddNormalCustomer(this);
					
                    break;


                case NormalCustomerStateEnum.Go_Out:

                    this.DestroyCustomer();

                    return;
            }

            this.NextStep();
        }


        private void DestroyCustomer()
        {
            /* 일반 손님 Destroy 처리 부분 */
        }

        #endregion

    }
}