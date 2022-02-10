# Tournaments to run: S4R1, S4R2, S4R3, S4R7, tmp/tmp10 (missiles), tmp/tmp6 (rockets/missiles), tmp/tmp9 (micro), tmp/tmp12 (ramming), tmp/tmp (mixed) (redo rockets with increased FiringTolerance tmp/tmp6)
# Run: ../parse_tournament_log_files_v1.13.7.py -q * && python3 -- ../weights.py */summary.csv -n
# Proposed weights give:
#  dmg ~= 4*dmgIn ~= 3*hits for medium guns ~= 2*hits for small guns ~= 4*hits for large guns
#  dmg ~= 4*dmgIn ~= 2*(str+hits) for rockets and missiles
#  k+a-d = α * (dmg+hits+rams), where α ~= 1 for normal planes, α ~= 0.7 for tanky planes, α ~= 2 for small planes and α ~= 3 for micro planes.
import argparse
import sys
from pathlib import Path
parser = argparse.ArgumentParser()
parser.add_argument('filenames', nargs='*', type=str, help="Tournament folders containing summary.csv files.")
parser.add_argument('-f', '--full', action='store_true', help="Use the full complement of craft instead of just the top half.")
parser.add_argument('-c', '--current', action='store_true', help="Compute the current score contributions instead.")
parser.add_argument('-sw', '--show-weights', action='store_true', help="Show the weights, then quit.")
args = parser.parse_args()

weights_def = [1, 0, 0, -1, 0, 0, 0, 0, 0, 0, 1, 2e-3, 2, 0, 0, 0, 0, 1, 2e-2, 0, 2e-4, 1e-4, 0.05, 0, 2e-3, 0, 1e-4, 5e-5, 0.5, 0, 0.01, 0, 2e-5, 1e-5, 0.01, 0]
weights = [1, 0, 0, -1, 0, 0, 0, 0, 0, 0, 1, 2e-3, 3, 0, 0, 0, 0, 1.5, 4e-3, 0, 1e-4, 4e-5, 0.035, 0, 6e-4, 0, 1.5e-4, 4.5e-5, 0.15, 0, 0.002, 0, 3e-5, 1.5e-5, 0.075, 0]
entries = {'deaths': 3, 'kills': 12, 'assists': 17, 'hit': 18, 'dmg': 20, 'dmgIn': 21, 'Rstr': 22, 'Rhit': 24, 'Rdmg': 26, 'RdmgIn': 27, 'Mstr': 28, 'Mhit': 30, 'Mdmg': 32, 'MdmgIn': 33, 'ram': 34}
if args.show_weights:
	for k, v in entries.items():
		print(k, weights[v])
	sys.exit()
current = {}
proposed = {}
for filename in args.filenames:
	with open(Path(filename) / 'summary.csv', 'r') as f:
		data = f.readlines()
		craft_count = data.index('\n') - 1
		scores = [[float(v) for v in row.strip().split(',')[2:-8]] for row in data[1:craft_count + 1]]
		rnds = len([l for l in data if "Per Round" in l][0].strip().split(',')) - 1
	curr = {k: [] for k in entries}
	prop = {k: [] for k in entries}
	for score in scores[:len(scores) // (1 if args.full else 2)]:  # Only take the top half (unless specified not to), assuming that the lower half has poor statistics due to low counts in the various fields. Including fewer increases the ratios and vice-versa.
		for k, v in entries.items():
			curr[k].append(score[v] * weights_def[v] / rnds)
		for k, v in entries.items():
			prop[k].append(score[v] * weights[v] / rnds)
	current[filename] = {k: round(sum(v) / len(v), 2) for k, v in curr.items()}
	proposed[filename] = {k: round(sum(v) / len(v), 2) for k, v in prop.items()}
if args.current:
	print('current:')
	for filename in current:
		print({k: v for k, v in current[filename].items() if v != 0}, filename)
	for filename in current:
		current[filename]['kda'] = round(sum(current[filename][k] for k in ('kills', 'deaths', 'assists')), 3)
		current[filename]['hd'] = round(sum(current[filename][k] for k in ('hit', 'dmg', 'dmgIn', 'Rstr', 'Rhit', 'Rdmg', 'RdmgIn', 'Mstr', 'Mhit', 'Mdmg', 'MdmgIn', 'ram')), 3)
		current[filename]['kda/hd'] = current[filename]['kda'] / current[filename]['hd']
	print(f"avg kda/hd current_ratio: {sum(current[filename]['kda/hd'] for filename in current)/len(current):.3f}")
	print("\n".join(f"{v['kda']:4g} / {v['hd']:4g} = {v['kda/hd']:6.4g} : {k}" for k, v in current.items()))
else:
	print('proposed:')
	for filename in proposed:
		print({k: v for k, v in proposed[filename].items() if v != 0}, filename)
	for filename in proposed:
		proposed[filename]['kda'] = round(sum(proposed[filename][k] for k in ('kills', 'deaths', 'assists')), 3)
		proposed[filename]['hd'] = round(sum(proposed[filename][k] for k in ('hit', 'dmg', 'dmgIn', 'Rstr', 'Rhit', 'Rdmg', 'RdmgIn', 'Mstr', 'Mhit', 'Mdmg', 'MdmgIn', 'ram')), 3)
		proposed[filename]['kda/hd'] = proposed[filename]['kda'] / proposed[filename]['hd']
	print(f"avg kda / hd: {sum(proposed[filename]['kda/hd'] for filename in proposed)/len(proposed):.3f}")
	print("\n".join(f"{v['kda']:4g} / {v['hd']:4g} = {v['kda/hd']:6.4g} : {k}" for k, v in proposed.items()))
