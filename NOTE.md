# DistributedActorTable Shutdown?

 - Cluster 가 down 되었거나 down 된 것처럼 보일 때 Container 는 어떻게 해야 하나?
   - Container 가 down 되면 해당 container 의 actor 가 죽은 것으로 간주한다.
   - down 된 것 처럼 보인다면 cluster 가 이미 actor 를 삭제 처리했을 것이므로 이 경우에는
     들고 있던 actor 를 모두 제거 하는 것이 옳다.
   - 정말 container 가 down 된 것이라면 어떻게 하나?
     이론적으로 container 가 새롭게 등장한 것이라면 들고 있는 actor 를 넘겨줄 수 있다.
     다만 이 경우 다른 container 와 race condition 이 발생할 수 있으므로 conflict 처리를 해줘야 한다.

# DistributedActorTable Rework (DONE)

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