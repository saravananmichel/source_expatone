"""Serial safe-synthetic benchmark. Models must already be downloaded; no private inputs."""
import os,asyncio,argparse,sys,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from evaluation.quality import run
from evaluation.support_challenge import main as challenge

async def main(args):
    for model in args.models.split(','):
        os.environ['OLLAMA_MODEL']=model
        await run(argparse.Namespace(provider='local',cases=args.cases))
        await challenge()

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--models',default='qwen3:4b,qwen3:8b,phi4-mini:latest');p.add_argument('--cases',default='contract,contradiction,cross-page,insurance')
    asyncio.run(main(p.parse_args()))
