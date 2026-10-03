"""Authorize and bound multipart requests BEFORE FastAPI spools them to disk."""
import asyncio
import hmac
import os
from starlette.responses import JSONResponse

MAX_REQUEST_BYTES=10*1024*1024+65536
class RequestTooLarge(Exception): pass

class IngestionLimits:
    def __init__(self, app): self.app=app
    async def __call__(self,scope,receive,send):
        if scope['type']!='http' or scope.get('path')!='/analyze':
            return await self.app(scope,receive,send)
        headers=dict(scope.get('headers',[]))
        expected=os.getenv('DOCUMENT_AI_SERVICE_KEY','').encode()
        if not expected or not hmac.compare_digest(headers.get(b'x-service-key',b''),expected):
            return await JSONResponse({'detail':'Unauthorized'},401)(scope,receive,send)
        try:
            length=int(headers.get(b'content-length',b'0'))
            if length>MAX_REQUEST_BYTES or length<0: raise RequestTooLarge()
            total=0
            async def bounded_receive():
                nonlocal total
                message=await receive()
                total+=len(message.get('body',b''))
                if total>MAX_REQUEST_BYTES: raise RequestTooLarge()
                return message
            async with asyncio.timeout(max(30,min(1800,int(os.getenv('DOCUMENT_AI_REQUEST_SECONDS','900'))))):
                await self.app(scope,bounded_receive,send)
        except TimeoutError:
            await JSONResponse({'detail':'Processing timed out'},503)(scope,receive,send)
        except (RequestTooLarge,ValueError):
            await JSONResponse({'detail':'Request too large'},413)(scope,receive,send)
