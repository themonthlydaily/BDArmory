#!/usr/bin/env python3

# Standard library imports
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

parser = argparse.ArgumentParser(description="Tournament log parser", formatter_class=argparse.ArgumentDefaultsHelpFormatter)
parser.add_argument('tournament', type=str, nargs='*', help="Tournament folder to parse.")
parser.add_argument('-q', '--quiet', action='store_true', help="Don't print results summary to console.")
parser.add_argument('-n', '--no-files', action='store_true', help="Don't create summary files.")
parser.add_argument('-s', '--score', action='store_false', help="Compute scores.")
parser.add_argument('-so', '--scores-only', action='store_true', help="Only display the scores in the summary on the console.")
parser.add_argument('-w', '--weights', type=str, default="1,0,0,-1.5,1,2e-3,3,1,5e-3,1e-5,0.1,2e-3,2e-5,0,0.5,0.01,1e-7,0,5e-2,0,0", help="Score weights (in order of main columns from 'Wins' to 'Ram').")
parser.add_argument('-c', '--current-dir', action='store_true', help="Parse the logs in the current directory as if it was a tournament without the folder structure.")
parser.add_argument('-nc', '--no-cumulative', action='store_true', help="Don't display cumulative scores at the end.")
parser.add_argument('-N', type=int, help="Only the first N logs in the folder (in -c mode).")
parser.add_argument('-z', '--zero-lowest-score', action='store_true', help="Shift the scores so that the lowest is 0.")
parser.add_argument('--show-weights', action='store_true', help="Display the score weights.")
args = parser.parse_args()
args.score = args.score or args.scores_only

if args.current_dir:
    tournamentDirs = [Path('')]
else:
    if len(args.tournament) == 0:
        tournamentDirs = None
        logsDir = Path(__file__).parent / "Logs"
        if logsDir.exists():
            tournamentFolders = list(logsDir.resolve().glob("Tournament*"))
            if len(tournamentFolders) > 0:
                tournamentFolders = sorted(list(dir for dir in tournamentFolders if dir.is_dir()))
            if len(tournamentFolders) > 0:
                tournamentDirs = [tournamentFolders[-1]]  # Latest tournament dir
        if tournamentDirs is None:  # Didn't find a tournament dir, revert to current-dir
            tournamentDirs = [Path('')]
            args.current_dir = True
    else:
        tournamentDirs = [Path(tournamentDir) for tournamentDir in args.tournament]  # Specified tournament dir

if args.score:
    try:
        weights = list(float(w) for w in args.weights.split(','))
    except:
        weights = []
    if len(weights) != 21:
        print('Invalid set of weights.')
        sys.exit()

if args.show_weights:
    fields = ('wins', 'survivedCount', 'miaCount', 'deathCount', 'deathOrder', 'deathTime', 'cleanKills', 'assists', 'hits', 'bulletDamage', 'rocketHits', 'rocketPartsHit', 'rocketDamage', 'rocketHitsTaken', 'missileHits', 'missilePartsHit', 'missileDamage', 'missileHitsTaken', 'ramScore', 'battleDamage', 'HPremaining')
    field_width = max(len(f) for f in fields)
    for w, f in zip(weights, fields):
        print(f"{f}:{' '*(field_width - len(f))} {w}")
    sys.exit()


def CalculateAccuracy(hits, shots): return 100 * hits / shots if shots > 0 else 0


def CalculateAvgHP(hp, heats): return hp / heats if heats > 0 else 0


def cumsum(l):
    v = 0
    for i in l:
        v += i
        yield v


for tournamentNumber, tournamentDir in enumerate(tournamentDirs):
    if tournamentNumber > 0 and not args.quiet:
        print("")
    tournamentData = {}
    for round in sorted(roundDir for roundDir in tournamentDir.iterdir() if roundDir.is_dir()) if not args.current_dir else (tournamentDir,):
        if not args.current_dir and len(round.name) == 0:
            continue
        tournamentData[round.name] = {}
        logFiles = sorted(round.glob("[0-9]*.log"))
        if len(logFiles) == 0:
            del tournamentData[round.name]
            continue
        for heat in logFiles if args.N == None else logFiles[:args.N]:
            with open(heat, "r") as logFile:
                tournamentData[round.name][heat.name] = {'result': None, 'duration': 0, 'craft': {}}
                for line in logFile:
                    line = line.strip()
                    if 'BDArmory.BDACompetitionMode' not in line:
                        continue  # Ignore irrelevant lines
                    _, field = line.split(' ', 1)
                    if field.startswith('Dumping Results'):
                        tournamentData[round.name][heat.name]['duration'] = float(field[field.find('(') + 4:field.find(')') - 1])
                    elif field.startswith('ALIVE:'):
                        state, craft = field.split(':', 1)
                        tournamentData[round.name][heat.name]['craft'][craft] = {'state': state}
                    elif field.startswith('DEAD:'):
                        state, order, time, craft = field.split(':', 3)
                        tournamentData[round.name][heat.name]['craft'][craft] = {'state': state, 'deathOrder': int(order), 'deathTime': float(time)}
                    elif field.startswith('MIA:'):
                        state, craft = field.split(':', 1)
                        tournamentData[round.name][heat.name]['craft'][craft] = {'state': state}
                    elif field.startswith('WHOSHOTWHOWITHGUNS:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'hitsBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHODAMAGEDWHOWITHGUNS:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'bulletDamageBy': {player: float(damage) for player, damage in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHOHITWHOWITHMISSILES:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'missileHitsBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHOPARTSHITWHOWITHMISSILES:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'missilePartsHitBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHODAMAGEDWHOWITHMISSILES:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'missileDamageBy': {player: float(damage) for player, damage in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHOHITWHOWITHROCKETS:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'rocketHitsBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHOPARTSHITWHOWITHROCKETS:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'rocketPartsHitBy': {player: int(hits) for player, hits in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHODAMAGEDWHOWITHROCKETS:'):
                        _, craft, shooters = field.split(':', 2)
                        data = shooters.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'rocketDamageBy': {player: float(damage) for player, damage in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHORAMMEDWHO:'):
                        _, craft, rammers = field.split(':', 2)
                        data = rammers.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'rammedPartsLostBy': {player: int(partsLost) for player, partsLost in zip(data[1::2], data[::2])}})
                    elif field.startswith('WHODAMAGEDWHOWITHBATTLEDAMAGE'):
                        _, craft, rammers = field.split(':', 2)
                        data = rammers.split(':')
                        tournamentData[round.name][heat.name]['craft'][craft].update({'battleDamageBy': {player: float(damage) for player, damage in zip(data[1::2], data[::2])}})
                    elif field.startswith('CLEANKILLGUNS:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanKillBy': killer})
                    elif field.startswith('CLEANKILLROCKETS:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRocketKillBy': killer})
                    elif field.startswith('CLEANKILLMISSILES:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanMissileKillBy': killer})
                    elif field.startswith('CLEANKILLRAMMING:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRamKillBy': killer})
                    elif field.startswith('HEADSHOTGUNS:'):  # FIXME make head-shots separate from clean-kills
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanKillBy': killer})
                    elif field.startswith('HEADSHOTROCKETS:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRocketKillBy': killer})
                    elif field.startswith('HEADSHOTMISSILE:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanMissileKillBy': killer})
                    elif field.startswith('HEADSHOTRAMMING:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRamKillBy': killer})
                    elif field.startswith('KILLSTEALGUNS:'):  # FIXME make kill-steals separate from clean-kills
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanKillBy': killer})
                    elif field.startswith('KILLSTEALROCKETS:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRocketKillBy': killer})
                    elif field.startswith('KILLSTEALMISSILE:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanMissileKillBy': killer})
                    elif field.startswith('KILLSTEALRAMMING:'):
                        _, craft, killer = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'cleanRamKillBy': killer})
                    elif field.startswith('GMKILL'):
                        _, craft, reason = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'GMKillReason': reason})
                    elif field.startswith('HPLEFT:'):
                        _, craft, hp = field.split(':', 2)
                        tournamentData[round.name][heat.name]['craft'][craft].update({'HPremaining': float(hp)})
                    elif field.startswith('ACCURACY:'):
                        _, craft, accuracy = field.split(':', 2)
                        hits, shots = accuracy.split('/')
                        accuracy = CalculateAccuracy(int(hits), int(shots))
                        tournamentData[round.name][heat.name]['craft'][craft].update({'accuracy': accuracy, 'hits': int(hits), 'shots': int(shots)})
                    elif field.startswith('RESULT:'):
                        heat_result = field.split(':', 2)
                        result_type = heat_result[1]
                        if (len(heat_result) > 2):
                            teams = json.loads(heat_result[2])
                            if isinstance(teams, dict):  # Win, single team
                                tournamentData[round.name][heat.name]['result'] = {'result': result_type, 'teams': {teams['team']: ', '.join(teams['members'])}}
                            elif isinstance(teams, list):  # Draw, multiple teams
                                tournamentData[round.name][heat.name]['result'] = {'result': result_type, 'teams': {team['team']: ', '.join(team['members']) for team in teams}}
                        else:  # Mutual Annihilation
                            tournamentData[round.name][heat.name]['result'] = {'result': result_type}
                    elif field.startswith('DEADTEAMS:'):
                        dead_teams = json.loads(field.split(':', 1)[1])
                        if len(dead_teams) > 0:
                            tournamentData[round.name][heat.name]['result'].update({'dead teams': {team['team']: ', '.join(team['members']) for team in dead_teams}})
                    # Ignore Tag mode for now.

    if not args.no_files and len(tournamentData) > 0:
        with open(tournamentDir / 'results.json', 'w') as outFile:
            json.dump(tournamentData, outFile, indent=2)

    craftNames = sorted(list(set(craft for round in tournamentData.values() for heat in round.values() for craft in heat['craft'].keys())))
    teamWins = Counter([team for round in tournamentData.values() for heat in round.values() if heat['result']['result'] == "Win" for team in heat['result']['teams']])
    teamDraws = Counter([team for round in tournamentData.values() for heat in round.values() if heat['result']['result'] == "Draw" for team in heat['result']['teams']])
    teamDeaths = Counter([team for round in tournamentData.values() for heat in round.values() if 'dead teams' in heat['result'] for team in heat['result']['dead teams']])
    teams = {team: members for round in tournamentData.values() for heat in round.values() if 'teams' in heat['result'] for team, members in heat['result']['teams'].items()}
    teams.update({team: members for round in tournamentData.values() for heat in round.values() if 'dead teams' in heat['result'] for team, members in heat['result']['dead teams'].items()})
    summary = {
        'craft': {
            craft: {
                'wins': len([1 for round in tournamentData.values() for heat in round.values() if heat['result']['result'] == "Win" and craft in next(iter(heat['result']['teams'].values())).split(", ")]),
                'survivedCount': len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'ALIVE']),
                'miaCount': len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'MIA']),
                'deathCount': (
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD']),  # Total
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanKillBy' in heat['craft'][craft]]),  # Bullets
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanRocketKillBy' in heat['craft'][craft]]),  # Rockets
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanMissileKillBy' in heat['craft'][craft]]),  # Missiles
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanRamKillBy' in heat['craft'][craft]]),  # Rams
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and not any(field in heat['craft'][craft] for field in ('cleanKillBy', 'cleanRocketKillBy', 'cleanMissileKillBy', 'cleanRamKillBy')) and any(field in heat['craft'][craft] for field in ('hitsBy', 'rocketPartsHitBy', 'missilePartsHitBy', 'rammedPartsLostBy'))]),  # Dirty kill
                    len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and not any(field in heat['craft'][craft] for field in ('hitsBy', 'rocketPartsHitBy', 'missilePartsHitBy', 'rammedPartsLostBy')) and not any('rammedPartsLostBy' in data and craft in data['rammedPartsLostBy'] for data in heat['craft'].values())]),  # Suicide (died without being hit or ramming anyone).
                ),
                'deathOrder': sum([heat['craft'][craft]['deathOrder'] / len(heat['craft']) if 'deathOrder' in heat['craft'][craft] else 1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft']]),
                'deathTime': sum([heat['craft'][craft]['deathTime'] if 'deathTime' in heat['craft'][craft] else heat['duration'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft']]),
                'cleanKills': (
                    len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if any((field in data and data[field] == craft) for field in ('cleanKillBy', 'cleanRocketKillBy', 'cleanMissileKillBy', 'cleanRamKillBy'))]),  # Total
                    len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanKillBy' in data and data['cleanKillBy'] == craft]),  # Bullets
                    len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanRocketKillBy' in data and data['cleanRocketKillBy'] == craft]),  # Rockets
                    len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanMissileKillBy' in data and data['cleanMissileKillBy'] == craft]),  # Missiles
                    len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if 'cleanRamKillBy' in data and data['cleanRamKillBy'] == craft]),  # Rams
                ),
                'assists': len([1 for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() if data['state'] == 'DEAD' and any(field in data and craft in data[field] for field in ('hitsBy', 'rocketPartsHitBy', 'missilePartsHitBy', 'rammedPartsLostBy')) and not any((field in data and data[field] == craft) for field in ('cleanKillBy', 'cleanRocketKillBy', 'cleanMissileKillBy', 'cleanRamKillBy'))]),
                'hits': sum([heat['craft'][craft]['hits'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'hits' in heat['craft'][craft]]),
                'bulletDamage': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('bulletDamageBy',) if field in data and craft in data[field]]),
                'rocketHits': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('rocketHitsBy',) if field in data and craft in data[field]]),
                'rocketHitsTaken': sum([sum(heat['craft'][craft]['rocketHitsBy'].values()) for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'rocketHitsBy' in heat['craft'][craft]]),
                'rocketPartsHit': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('rocketPartsHitBy',) if field in data and craft in data[field]]),
                'rocketDamage': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('rocketDamageBy',) if field in data and craft in data[field]]),
                'missileHits': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('missileHitsBy',) if field in data and craft in data[field]]),
                'missileHitsTaken': sum([sum(heat['craft'][craft]['missileHitsBy'].values()) for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'missileHitsBy' in heat['craft'][craft]]),
                'missilePartsHit': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('missilePartsHitBy',) if field in data and craft in data[field]]),
                'missileDamage': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('missileDamageBy',) if field in data and craft in data[field]]),
                'ramScore': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for data in heat['craft'].values() for field in ('rammedPartsLostBy',) if field in data and craft in data[field]]),
                'battleDamage': sum([data[field][craft] for round in tournamentData.values() for heat in round.values() for player, data in heat['craft'].items() if player != craft for field in ('battleDamageBy',) if field in data and craft in data[field]]),
                'battleDamageTaken': sum([sum(heat['craft'][craft]['battleDamageBy'].values()) for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'battleDamageBy' in heat['craft'][craft]]),
                'HPremaining': CalculateAvgHP(sum([heat['craft'][craft]['HPremaining'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'HPremaining' in heat['craft'][craft] and heat['craft'][craft]['state'] == 'ALIVE']), len([1 for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'ALIVE'])),
                'accuracy': CalculateAccuracy(sum([heat['craft'][craft]['hits'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'hits' in heat['craft'][craft]]), sum([heat['craft'][craft]['shots'] for round in tournamentData.values() for heat in round.values() if craft in heat['craft'] and 'shots' in heat['craft'][craft]])),
            }
            for craft in craftNames
        },
        'team results': {
            'wins': teamWins,
            'draws': teamDraws,
            'deaths': teamDeaths
        },
        'teams': teams
    }

    for craft in summary['craft'].values():
        spawns = craft['survivedCount'] + craft['deathCount'][0]
        craft.update({
            'damage/hit': craft['bulletDamage'] / craft['hits'] if craft['hits'] > 0 else 0,
            'hits/spawn': craft['hits'] / spawns if spawns > 0 else 0,
            'damage/spawn': craft['bulletDamage'] / spawns if spawns > 0 else 0,
        })

    if args.score:
        for craft in summary['craft'].values():
            craft.update({
                'score':
                weights[0] * craft['wins'] +
                weights[1] * craft['survivedCount'] +
                weights[2] * craft['miaCount'] +
                weights[3] * craft['deathCount'][0] +
                weights[4] * craft['deathOrder'] +
                weights[5] * craft['deathTime'] +
                weights[6] * craft['cleanKills'][0] +
                weights[7] * craft['assists'] +
                weights[8] * craft['hits'] +
                weights[9] * craft['bulletDamage'] +
                weights[10] * craft['rocketHits'] +
                weights[11] * craft['rocketPartsHit'] +
                weights[12] * craft['rocketDamage'] +
                weights[13] * craft['rocketHitsTaken'] +
                weights[14] * craft['missileHits'] +
                weights[15] * craft['missilePartsHit'] +
                weights[16] * craft['missileDamage'] +
                weights[17] * craft['missileHitsTaken'] +
                weights[18] * craft['ramScore'] +
                weights[19] * craft['battleDamage'] +
                weights[20] * craft['HPremaining']
            })
        if args.zero_lowest_score:
            offset = min(craft['score'] for craft in summary['craft'].values())
            for craft in summary['craft'].values():
                craft['score'] -= offset

    if not args.no_files and len(summary['craft']) > 0:
        with open(tournamentDir / 'summary.json', 'w') as outFile:
            json.dump(summary, outFile, indent=2)

    if len(summary['craft']) > 0:
        if not args.no_files:
            headers = (["score", ] if args.score else []) + [k for k in next(iter(summary['craft'].values())).keys() if k not in ('score',)]
            csv_summary = ["craft," + ",".join(
                ",".join(('deathCount', 'dcB', 'dcR', 'dcM', 'dcR', 'dcA', 'dcS')) if k == 'deathCount' else
                ",".join(('cleanKills', 'ckB', 'ckR', 'ckM', 'ckR')) if k == 'cleanKills' else
                k for k in headers), ]
            for craft, score in sorted(summary['craft'].items(), key=lambda i: i[1]['score'], reverse=True):
                csv_summary.append(craft + "," + ",".join(str(int(100 * score[h]) / 100) if not isinstance(score[h], tuple) else ",".join(str(int(100 * sf) / 100) for sf in score[h]) for h in headers))
            # Write main summary results to the summary.csv file.
            with open(tournamentDir / 'summary.csv', 'w') as outFile:
                outFile.write("\n".join(csv_summary))

        teamNames = sorted(list(set([team for result_type in summary['team results'].values() for team in result_type])))
        default_team_names = [chr(k) for k in range(ord('A'), ord('A') + len(summary['craft']))]

        if args.score:  # Per round scores.
            per_round_summary = {
                craft: [
                    {
                        'wins': len([1 for heat in round.values() if heat['result']['result'] == "Win" and craft in next(iter(heat['result']['teams'].values())).split(", ")]),
                        'survivedCount': len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'ALIVE']),
                        'miaCount': len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'MIA']),
                        'deathCount': (
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD']),  # Total
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanKillBy' in heat['craft'][craft]]),  # Bullets
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanRocketKillBy' in heat['craft'][craft]]),  # Rockets
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanMissileKillBy' in heat['craft'][craft]]),  # Missiles
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and 'cleanRamKillBy' in heat['craft'][craft]]),  # Rams
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and not any(field in heat['craft'][craft] for field in ('cleanKillBy', 'cleanRocketKillBy', 'cleanMissileKillBy', 'cleanRamKillBy')) and any(field in heat['craft'][craft] for field in ('hitsBy', 'rocketPartsHitBy', 'missilePartsHitBy', 'rammedPartsLostBy'))]),  # Dirty kill
                            len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'DEAD' and not any(field in heat['craft'][craft] for field in ('hitsBy', 'rocketPartsHitBy', 'missilePartsHitBy', 'rammedPartsLostBy')) and not any('rammedPartsLostBy' in data and craft in data['rammedPartsLostBy'] for data in heat['craft'].values())]),  # Suicide (died without being hit or ramming anyone).
                        ),
                        'deathOrder': sum([heat['craft'][craft]['deathOrder'] / len(heat['craft']) if 'deathOrder' in heat['craft'][craft] else 1 for heat in round.values() if craft in heat['craft']]),
                        'deathTime': sum([heat['craft'][craft]['deathTime'] if 'deathTime' in heat['craft'][craft] else heat['duration'] for heat in round.values() if craft in heat['craft']]),
                        'cleanKills': (
                            len([1 for heat in round.values() for data in heat['craft'].values() if any((field in data and data[field] == craft) for field in ('cleanKillBy', 'cleanRocketKillBy', 'cleanMissileKillBy', 'cleanRamKillBy'))]),  # Total
                            len([1 for heat in round.values() for data in heat['craft'].values() if 'cleanKillBy' in data and data['cleanKillBy'] == craft]),  # Bullets
                            len([1 for heat in round.values() for data in heat['craft'].values() if 'cleanRocketKillBy' in data and data['cleanRocketKillBy'] == craft]),  # Rockets
                            len([1 for heat in round.values() for data in heat['craft'].values() if 'cleanMissileKillBy' in data and data['cleanMissileKillBy'] == craft]),  # Missiles
                            len([1 for heat in round.values() for data in heat['craft'].values() if 'cleanRamKillBy' in data and data['cleanRamKillBy'] == craft]),  # Rams
                        ),
                        'assists': len([1 for heat in round.values() for data in heat['craft'].values() if data['state'] == 'DEAD' and any(field in data and craft in data[field] for field in ('hitsBy', 'rocketPartsHitBy', 'missilePartsHitBy', 'rammedPartsLostBy')) and not any((field in data and data[field] == craft) for field in ('cleanKillBy', 'cleanRocketKillBy', 'cleanMissileKillBy', 'cleanRamKillBy'))]),
                        'hits': sum([heat['craft'][craft]['hits'] for heat in round.values() if craft in heat['craft'] and 'hits' in heat['craft'][craft]]),
                        'bulletDamage': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('bulletDamageBy',) if field in data and craft in data[field]]),
                        'rocketHits': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('rocketHitsBy',) if field in data and craft in data[field]]),
                        'rocketHitsTaken': sum([sum(heat['craft'][craft]['rocketHitsBy'].values()) for heat in round.values() if craft in heat['craft'] and 'rocketHitsBy' in heat['craft'][craft]]),
                        'rocketPartsHit': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('rocketPartsHitBy',) if field in data and craft in data[field]]),
                        'rocketDamage': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('rocketDamageBy',) if field in data and craft in data[field]]),
                        'missileHits': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('missileHitsBy',) if field in data and craft in data[field]]),
                        'missileHitsTaken': sum([sum(heat['craft'][craft]['missileHitsBy'].values()) for heat in round.values() if craft in heat['craft'] and 'missileHitsBy' in heat['craft'][craft]]),
                        'missilePartsHit': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('missilePartsHitBy',) if field in data and craft in data[field]]),
                        'missileDamage': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('missileDamageBy',) if field in data and craft in data[field]]),
                        'ramScore': sum([data[field][craft] for heat in round.values() for data in heat['craft'].values() for field in ('rammedPartsLostBy',) if field in data and craft in data[field]]),
                        'battleDamage': sum([data[field][craft] for heat in round.values() for player, data in heat['craft'].items() if player != craft for field in ('battleDamageBy',) if field in data and craft in data[field]]),
                        'battleDamageTaken': sum([sum(heat['craft'][craft]['battleDamageBy'].values()) for heat in round.values() if craft in heat['craft'] and 'battleDamageBy' in heat['craft'][craft]]),
                        'HPremaining': CalculateAvgHP(sum([heat['craft'][craft]['HPremaining'] for heat in round.values() if craft in heat['craft'] and 'HPremaining' in heat['craft'][craft] and heat['craft'][craft]['state'] == 'ALIVE']), len([1 for heat in round.values() if craft in heat['craft'] and heat['craft'][craft]['state'] == 'ALIVE'])),
                        'accuracy': CalculateAccuracy(sum([heat['craft'][craft]['hits'] for heat in round.values() if craft in heat['craft'] and 'hits' in heat['craft'][craft]]), sum([heat['craft'][craft]['shots'] for heat in round.values() if craft in heat['craft'] and 'shots' in heat['craft'][craft]])),
                    } for round in tournamentData.values()
                ] for craft in craftNames
            }
            per_round_scores = {
                craft: [
                    weights[0] * scores[round]['wins'] +
                    weights[1] * scores[round]['survivedCount'] +
                    weights[2] * scores[round]['miaCount'] +
                    weights[3] * scores[round]['deathCount'][0] +
                    weights[4] * scores[round]['deathOrder'] +
                    weights[5] * scores[round]['deathTime'] +
                    weights[6] * scores[round]['cleanKills'][0] +
                    weights[7] * scores[round]['assists'] +
                    weights[8] * scores[round]['hits'] +
                    weights[9] * scores[round]['bulletDamage'] +
                    weights[10] * scores[round]['rocketHits'] +
                    weights[11] * scores[round]['rocketPartsHit'] +
                    weights[12] * scores[round]['rocketDamage'] +
                    weights[13] * scores[round]['rocketHitsTaken'] +
                    weights[14] * scores[round]['missileHits'] +
                    weights[15] * scores[round]['missilePartsHit'] +
                    weights[16] * scores[round]['missileDamage'] +
                    weights[17] * scores[round]['missileHitsTaken'] +
                    weights[18] * scores[round]['ramScore'] +
                    weights[19] * scores[round]['battleDamage'] +
                    weights[20] * scores[round]['HPremaining']
                    for round in range(len(scores))]
                for craft, scores in per_round_summary.items()
            }

        if not args.quiet:  # Write results to console
            strings = []
            headers = ['Name', 'Wins', 'Survive', 'MIA', 'Deaths (BRMRAS)', 'D.Order', 'D.Time', 'Kills (BRMR)', 'Assists', 'Hits', 'Damage', 'RocHits', 'RocParts', 'RocDmg', 'HitByRoc', 'MisHits', 'MisParts', 'MisDmg', 'HitByMis', 'Ram', 'BD dealt', 'BD taken', 'Acc%', 'HP%', 'Dmg/Hit', 'Hits/Sp', 'Dmg/Sp'] if not args.scores_only else ['Name']
            if args.score:
                headers.insert(1, 'Score')
            summary_strings = {'header': {field: field for field in headers}}
            for craft in sorted(summary['craft']):
                tmp = summary['craft'][craft]
                spawns = tmp['survivedCount'] + tmp['deathCount'][0]
                summary_strings.update({
                    craft: {
                        'Name': craft,
                        'Wins': f"{tmp['wins']}",
                        'Survive': f"{tmp['survivedCount']}",
                        'MIA': f"{tmp['miaCount']}",
                        'Deaths (BRMRAS)': f"{tmp['deathCount'][0]} ({' '.join(str(s) for s in tmp['deathCount'][1:])})",
                        'D.Order': f"{tmp['deathOrder']:.3f}",
                        'D.Time': f"{tmp['deathTime']:.1f}",
                        'Kills (BRMR)': f"{tmp['cleanKills'][0]} ({' '.join(str(s) for s in tmp['cleanKills'][1:])})",
                        'Assists': f"{tmp['assists']}",
                        'Hits': f"{tmp['hits']}",
                        'Damage': f"{tmp['bulletDamage']:.0f}",
                        'RocHits': f"{tmp['rocketHits']}",
                        'RocParts': f"{tmp['rocketPartsHit']}",
                        'RocDmg': f"{tmp['rocketDamage']:.0f}",
                        'HitByRoc': f"{tmp['rocketHitsTaken']}",
                        'MisHits': f"{tmp['missileHits']}",
                        'MisParts': f"{tmp['missilePartsHit']}",
                        'MisDmg': f"{tmp['missileDamage']:.0f}",
                        'HitByMis': f"{tmp['missileHitsTaken']}",
                        'Ram': f"{tmp['ramScore']}",
                        'BD dealt': f"{tmp['battleDamage']:.0f}",
                        'BD taken': f"{tmp['battleDamageTaken']:.0f}",
                        'Acc%': f"{tmp['accuracy']:.2f}",
                        'HP%': f"{tmp['HPremaining']:.2f}",
                        'Dmg/Hit': f"{tmp['damage/hit']:.1f}",
                        'Hits/Sp': f"{tmp['hits/spawn']:.1f}",
                        'Dmg/Sp': f"{tmp['damage/spawn']:.1f}"
                    }
                })
                if args.score:
                    summary_strings[craft]['Score'] = f"{tmp['score']:.3f}"
            columns_to_show = [header for header in headers if not all(craft[header] == "0" for craft in list(summary_strings.values())[1:])]
            column_widths = {column: max(len(craft[column]) + 2 for craft in summary_strings.values()) for column in headers}
            strings.append(''.join(f"{header:{column_widths[header]}s}" for header in columns_to_show))
            for craft in sorted(summary['craft'], key=None if not args.score else lambda craft: summary['craft'][craft]['score'], reverse=False if not args.score else True):
                strings.append(''.join(f"{summary_strings[craft][header]:{column_widths[header]}s}" for header in columns_to_show))

            # Teams summary
            if len(teamNames) > 0 and not all(name in default_team_names for name in teamNames):  # Don't do teams if they're assigned as 'A', 'B', ... as they won't be consistent between rounds.
                name_length = max([len(team) for team in teamNames])
                strings.append(f"\nTeam{' '*(name_length-4)}\tWins\tDraws\tDeaths\tVessels")
                for team in sorted(teamNames, key=lambda team: teamWins[team], reverse=True):
                    strings.append(f"{team}{' '*(name_length-len(team))}\t{teamWins[team]}\t{teamDraws[team]}\t{teamDeaths[team]}\t{summary['teams'][team]}")

            # Per round cumulative score
            if args.score and not args.no_cumulative:
                name_length = max([len(name) for name in per_round_scores.keys()] + [23])
                strings.append(f"\nName \\ Cumulative Score{' '*(name_length-23)}\t" + "\t".join(f"{r:>7d}" for r in range(len(next(iter(per_round_scores.values()))))))
                strings.append('\n'.join(f"{craft}:{' '*(name_length-len(craft))}\t" + "\t".join(f"{s:>7.3g}" for s in cumsum(per_round_scores[craft])) for craft in sorted(per_round_scores, key=lambda craft: summary['craft'][craft]['score'], reverse=True)))

            # Print stuff to the console.
            for string in strings:
                print(string)

        # Write teams results to the summary.csv file.
        if not args.no_files:
            with open(tournamentDir / 'summary.csv', 'a') as f:
                f.write('\n\nTeam,Wins,Draws,Deaths,Vessels')
                for team in sorted(teamNames, key=lambda team: teamWins[team], reverse=True):
                    f.write('\n' + ','.join([str(v) for v in (team, teamWins[team], teamDraws[team], teamDeaths[team], summary['teams'][team].replace(", ", ","))]))

                # Write per round cumulative score results to summary.csv file.
                if args.score and not args.no_cumulative:
                    f.write(f"\n\nName \\ Cumulative Score Per Round," + ",".join(f"{r:>7d}" for r in range(len(next(iter(per_round_scores.values()))))))
                    for craft in sorted(per_round_scores, key=lambda craft: summary['craft'][craft]['score'], reverse=True):
                        f.write(f"\n{craft}," + ",".join(f"{s:.3g}" for s in cumsum(per_round_scores[craft])))

    else:
        print(f"No valid log files found in {tournamentDir}.")
