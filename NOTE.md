# DistributedActorDictionary Redesign

 - DistributedActorDictionaryCenter 가 Center 를 빼고 메인이 되어야 한다.
   - 이게 외부에서 보이는 기본 Actor 여야 함. 이거 사각이었다. Node 는 implementation detail 이지.

 - 두가지 use case 는?

   - RoomTable 식
     - 외부에서 Create 요청을 하면 Dictionary 가 알아서 round-robin 으로 어딘가에 해당 actor 를
	   만들고 IActorRef 를 반환한다. 스스로 해당 actor 를 만들어서 등록하는 경우 없음
   - UserTable 식
     - Worker Node 에서 직접 만들어서 Dictionary 에 등록.
	 - 추후에 RoomTable 처럼 사용될 수도 있다. (Webby 에서 접근할 떄?)
	   - 그런데 이것도 결국 UserTable 로 할 수 있겠네. 그렇다면 좀 더 API 를 타이트하게
	     만들 수 있긴 하다. (둘의 api 가 살짝 다르니..)

 - API.
   - Dictionary
     - For User
       - Create      : Worker 에게 Create 요청 후 등록
	   - GetOrCreate : 있으면 Get. 없으면 Create
	   - Get
     - For Worker
	   - Created     : 생성 요청에 대한 완료 알림
	   - Removed     : 소유하고 있는 Actor 삭제 알림
   - Worker
     - For User
	   - Add         : 직접 Actor 를 생성하고 등록시키는 역할
	   - Remove      : Actor 가 소멸이 아닌 능동적으로 빠져나가려고 할 떄 사용
	 - For Dictionary
	   - Create      : 생성 요청

 - 적용
   - RoomTable
     - Dict 와 Worker 가 있다.
	 - Dict 가 GetOrCreate 요청을 받으면 Worker 와 함꼐 처리
	 - Room 이 소멸되면 Worker 가 받아 Dict 에게 알림.
   - UserTable
     - User 는 ClientGateway 에 의해 생성됨.
	 - 생성된 User 는 Worker or Dict 에게 등록 요청을 보냄.
	   - Worker 가 다시 