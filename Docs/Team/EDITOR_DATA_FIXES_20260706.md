# 에디터 데이터 보수 내역 및 남은 작업 (2026-07-06)

## 이미 처리된 데이터 보수 (커밋 포함)

| 대상 | 내용 | 커밋 |
|---|---|---|
| GameManager.prefab | RoomManager `allMapRooms`에 aut_3, Ending 추가 / spr_4.prefab 북쪽 문 nextRoom을 Ending(알파 레거시) → sum_1로 변경 | `bd159cb` |
| FIeld.unity | `allMapRooms` 인스턴스 오버라이드 재구성(17개) — sum_1, sum_2 추가. debugLogs 강제 off 오버라이드 제거 | `d51b43f` |
| MainCanvas.prefab | 리네임된 InGameMenu 인스턴스(하이어라키명 "CraftingMenu")의 m_IsActive 오버라이드 0으로 — 씬 로드 시 메뉴가 화면을 덮던 원인 | `d51b43f` |

주의: MainCanvas 안에는 이름이 "CraftingMenu"인 오브젝트가 **2개**입니다 (진짜 CraftingMenu.prefab 인스턴스 + CraftingMenu로 리네임된 InGameMenu.prefab 인스턴스). 이름으로 검색할 때 혼동 주의.

## 남은 후속 작업 (에디터/기획 — 코드 방어는 이미 있음)

1. **방 프리팹 Grid/TileCollider 구조 보수**: 지면 타일이 테두리만 감싸는 방은 전환 목적지가 벽 바깥에 떨어질 수 있음. 현재는 코드 롤백 + QA 감시로 방어 중이지만, 각 방의 통로 앞 지면 타일을 채우는 것이 정석.
2. **문틈 몬스터 물리 차단**: 몬스터가 문틈에 서 있는 것까지 막으려면 방 프리팹 문 위치에 Enemy 레이어 전용 콜라이더 필요 (기획 결정 필요).
3. **보스 투사체 prefab collider 실측**: QA 콘솔 F4 시각화로 스프라이트 대비 콜라이더 크기 확인 후 프리팹 조정.
4. **firewall 등 투사체 시각 이상 조사**: 최근 코드 변경과 무관 확인됨(렌더링 코드 무접촉). 후보: F4 오버레이가 켜져 있었던 것 / 정밀 판정으로 투사체가 몸을 겹치고 지나가는 기대된 체감 변화 / 과거 shader·material GUID 충돌 재발. 배제 후에도 이상하면 해당 프리팹의 머티리얼/셰이더 확인.
5. **좌표 혼재 정리 (기획 대기)**: 동일 좌표에 복수 방(조건부 스토리 연결 예정)은 linear 스토리 기획 확정 후 정리. 미참조 고아 에셋 `spr_1 BOSS.asset` 처리 포함.
6. **레이/지속형 투사체 정밀 판정 확대 여부**: AquaRay, BriefCandleRay, ElectricLaserRay, BedimmedWall, CarmaExcisionTrueHitbox — 현재는 본체 판정 유지 중. 난이도 체감 확인 후 결정.
