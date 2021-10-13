# Standard library imports
import argparse
import json
from collections import Counter
from pathlib import Path

parser = argparse.ArgumentParser(description="Parse results.json of a N-choose-K style tournament producing a table of who-beat-who.", formatter_class=argparse.ArgumentDefaultsHelpFormatter)
parser.add_argument('results', type=str, help="results.json file to parse.")
parser.add_argument('-o', '--output', default="n-choose-k.csv", help="File to output CSV to.")
args = parser.parse_args()

results_file = Path(args.results)
if not results_file.exists():
    print(f"File not found: {results_file}")
else:
    with open(results_file, 'r') as f:
        data = json.load(f)
    counts = Counter([(next(iter(heat['result']['teams'].keys())), next(iter(heat['result']['dead teams'].keys()))) for Round in data.values() for heat in Round.values() if heat['result']['result'] == 'Win'])
    A = set(k[0] for k in counts.keys())
    B = set(k[1] for k in counts.keys())
    names = sorted(A.union(B))
    name_map = {n: c for c, n in enumerate(names)}
    t = [[0] * len(names) for i in range(len(names))]
    for k, c in counts.items():
        t[name_map[k[0]]][name_map[k[1]]] = c
    with open(args.output, 'w') as f:
        f.write("vs," + ",".join(names) + "\n")
        for i, name in enumerate(names):
            f.write(name + "," + ",".join([str(c) for c in t[i]]) + "\n")
