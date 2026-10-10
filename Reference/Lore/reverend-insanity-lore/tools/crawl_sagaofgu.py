import os,re,sys,time,urllib.request,concurrent.futures as cf
root=sys.argv[1]; sm=sys.argv[2]
urls=re.findall(r'<loc>([^<]*)</loc>',open(sm,encoding='utf-8').read())
order={'gu':0,'characters':1,'recipes':2,'factions':3,'locations':4,'regions':5,'materials':6,'moves':7,'cultivation':8,'inheritances':9,'gu-houses':10,'paths':11,'races':12,'legends':13,'earths':14,'heavens':15,'eras':16,'arcs':17,'fights':18,'chapters':19}
def key(u):
    p=u.replace('https://sagaofgu.com/','').strip('/').split('/')
    return (order.get(p[0],50),u)
urls.sort(key=key)
def fetch(u):
    p=u.replace('https://sagaofgu.com/','').strip('/') or 'index'
    parts=p.split('/'); d=os.path.join(root,parts[0]); os.makedirs(d,exist_ok=True)
    f=os.path.join(d,('_'.join(parts[1:]) or '_index')+'.html')
    if os.path.exists(f) and os.path.getsize(f)>1000: return 0
    for a in range(4):
        try:
            r=urllib.request.urlopen(urllib.request.Request(u,headers={'User-Agent':'GUWorld-lore-research/1.0 (polite; 2 req/s)'}),timeout=30)
            data=r.read(); open(f,'wb').write(data); time.sleep(0.3); return 1
        except Exception as e:
            time.sleep(3*(a+1)); err=str(e)
    print('FAIL',u,err,flush=True); return 0
n=0
with cf.ThreadPoolExecutor(4) as ex:
    for i,res in enumerate(ex.map(fetch,urls)):
        n+=res
        if i%100==0: print(i,len(urls),n,flush=True)
print('DONE',len(urls),n,flush=True)
