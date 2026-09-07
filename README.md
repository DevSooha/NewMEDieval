# MEDieval - 약제사 이야기

두 재료를 조합한 포션으로 탄막을 전개하며 적과 보스를 상대하는 **Unity 기반 2D 탑다운 전투 RPG**입니다. 재료 조합에 따라 탄막 패턴과 속성, 피격 효과가 달라집니다.

| 항목 | 내용 |
| --- | --- |
| 개발 기간 | 2025.11 ~ 진행 중 |
| 개발 환경 | Unity 6 · C# · Git |
| 에디터 버전 | 6000.2.9f1 |
| 개발 구성 | 기획·아트·프로그래밍 팀 협업, 프로그래밍 3인 |
| 진행 상태 | 동아리 알파테스트와 4개 팀 합동 시연회 진행, 출시를 위한 보완 중 |

## 주요 기능

- 재료 수집과 포션 제조, 인벤토리 및 무기 슬롯 장착
- 포션 조합에 따른 폭탄·탄막 공격과 피해·상태 효과
- 현재 방과 인접 방을 관리하는 필드 시스템
- 보스별 공격 패턴과 전투 연출
- 플레이어·인벤토리·장착 상태·월드 상태 저장 및 복원

## 주요 구현

### 재료별 공격을 시간순으로 실행하는 포션 전투

포션에는 서로 다른 두 재료의 공격 규칙이 함께 들어갑니다. 각 재료는 발사 시점과 탄막 패턴, 명중 효과를 가집니다.

각 재료의 발사 시각·패턴·공격 데이터를 이벤트로 구성하고, 이를 시간순으로 정렬해 실행합니다. 생성된 탄막의 이동과 장판 전환, 피해·상태 효과는 별도 코드에서 처리합니다.

- [BombPatternSequenceRunner.cs — 발사 일정 구성과 실행](Assets/Scripts/Potion%26Bomb/BombPatternSequenceRunner.cs)
- [PotionHitResolver.cs — 포션 피격 처리](Assets/Scripts/Potion%26Bomb/PotionHitResolver.cs)

### 방 준비와 객체 생명주기를 고려한 저장·복원

씬 로드와 방 오브젝트 준비는 같은 시점에 끝나지 않습니다. 방 시스템의 준비 완료 이벤트 이후 월드 상태를 적용하고, 씬이 다시 구성되면 현재 존재하는 저장 대상 객체를 찾아 복원합니다.

- [SaveManager.cs — 저장 데이터와 복원 순서](Assets/Scripts/SaveSystem/SaveManager.cs)
- [RoomManager.cs — 방 초기화와 이동](Assets/Scripts/Field/RoomManager.cs)
- [RoomData.cs — 방 데이터](Assets/Scripts/Field/RoomData.cs)

### 보스 패턴과 전투 종료 처리

보스별 공격 패턴에 VFX와 애니메이션을 연결합니다. 공통 전투 로직에서는 전투 종료 시 남은 투사체와 상태를 정리합니다.

- [BossCombatBase.cs — 보스전 공통 처리](Assets/Scripts/BossFights/BossCombatBase.cs)
- [BossFights — 보스 관련 코드](Assets/Scripts/BossFights)

## 프로젝트 열기

1. 저장소를 클론합니다.
2. Unity Hub에서 저장소 루트를 프로젝트로 추가합니다.
3. [ProjectVersion.txt](ProjectSettings/ProjectVersion.txt)에 명시된 Unity 버전으로 엽니다.

에디터 버전이 변경되면 `ProjectVersion.txt`를 기준으로 사용합니다.

<details>
<summary>팀 협업 규칙과 프로그래머 구성</summary>

## 협업 규칙
- 기능 개발 시 feature 브랜치를 생성 후 작업합니다.
- 각자 브랜치에서 작업 후, 충돌이 없는 경우에만 main 브랜치에 병합합니다.
- 개인 기본 브랜치는 아래와 같이 사용합니다.
  - 수하: `sooha`
  - 현서: `hyunseo`
  - 혜교: `hyegyo`
- 커밋 메시지는 명확하고 구체적으로 작성합니다.
- `Library/`, `Temp/`, `Build/` 등 불필요한 파일은 `.gitignore`로 관리됩니다.  
  (절대 커밋하지 않습니다.)

---

## 팀원 
- 수하
- 현서
- 혜교


</details>

