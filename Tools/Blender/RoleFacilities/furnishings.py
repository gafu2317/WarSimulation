"""Purpose-built usable furniture, tools and courtyard equipment."""
import math


def stairs(m,x,z,width,height=.45,depth=1.0):
    for i in range(3):
        h=height*(i+1)/3
        m.box((x,h/2,z+depth*i/3),(width,h,depth*(1-i/3)),'trim')


def bench(m,x,z,width=1.8,y=.2):
    m.box((x,y+.48,z),(width,.13,.48),'wood')
    for side in (-1,1):
        m.box((x+side*width*.37,y+.23,z),(.13,.46,.38),'timber')
        m.box((x+side*width*.37,y+.80,z+.2),(.1,.7,.1),'timber')
    m.box((x,y+.95,z+.22),(width,.22,.1),'wood')


def table(m,x,z,w=1.8,d=.85,y=.2,cloth=False):
    m.box((x,y+.82,z),(w,.15,d),'wood')
    for side in (-1,1):
        for end in (-1,1):m.box((x+side*w*.38,y+.4,z+end*d*.32),(.13,.8,.13),'timber')
    if cloth:m.box((x,y+.91,z),(w*.8,.025,d*.88),'green')


def barrel(m,x,z,r=.42,y=.2):
    m.lathe(x,y,z,[(r*.86,0),(r*.94,.18),(r,.52),(r*.94,.87),(r*.86,1.04)],'wood',n=24)
    for h,radius in ((.12,r*.93),(.49,r),(.86,r*.94)):
        m.ring(x,z,radius-.025,radius+.025,y+h,y+h+.055,'metal',n=24)
    for dx in (-.45,0,.45):m.box((x+dx*r,y+1.046,z),(.018,.012,r*1.40),'timber')


def vessel(m,x,y,z,r=.16,h=.6,color='glass'):
    m.lathe(x,y,z,[(r*.72,0),(r,.10*h),(r,.57*h),(r*.43,.73*h),(r*.40,.88*h),(r*.49,.9*h)],color,n=20)
    m.cylinder((x,y+h*.94,z),r*.35,h*.14,'wood',n=12)


def shelf(m,x,z,w=2,h=2.7,y=.2,books=True):
    m.box((x,y+h/2,z+.24),(w,h,.12),'timber')
    for side in (-1,1):m.box((x+side*w/2,y+h/2,z),(.12,h,.6),'wood')
    levels=max(2,int(h/.65))
    for row in range(levels+1):m.box((x,y+row*h/levels,z),(w,.09,.6),'wood')
    for row in range(levels):
        count=max(4,int(w/.18))
        for i in range(count):
            xx=x+(i-(count-1)/2)*w/(count+1)
            yy=y+row*h/levels+.06
            if books:
                height=h/levels*(.58+(i*7+row*3)%5*.055)
                m.box((xx,yy+height/2,z-.08),(w/(count+2),height,.34),('red','blue','green','wood')[(i+row)%4])
                m.box((xx,yy+height*.68,z-.255),(w/(count+2)*.75,.018,.012),'paper')
            elif i%2==0:vessel(m,xx,yy,z-.1,r=.09,h=.34,color='glass')


def reading_desk(m,x,z,w=1.6,y=.2):
    table(m,x,z,w,.8,y)
    m.box((x,y+.91,z),(w*.55,.055,.50),'paper')
    m.box((x,y+.95,z+.01),(.035,.035,.48),'wood')
    bench(m,x,z-.85,w*.72,y)


def weapon_rack(m,x,z,w=2.2,y=.2,shields=False):
    for side in (-1,1):m.box((x+side*w/2,y+1.05,z),(.14,2.1,.18),'timber')
    for yy in (.45,1.65):m.box((x,y+yy,z),(w+.16,.12,.18),'wood')
    for i in range(4):
        xx=x+(i-1.5)*w*.22
        if shields:
            m.cylinder((xx,y+1.12,z-.16),.31,.12,'wood',n=12,axis=(0,0,1))
            m.cylinder((xx,y+1.12,z-.24),.11,.08,'metal',n=12,axis=(0,0,1))
        else:
            m.beam((xx,y+.4,z-.14),(xx,y+1.75,z-.14),.07,'metal',.025)
            m.box((xx,y+.69,z-.14),(.35,.055,.1),'metal')
            m.box((xx,y+.44,z-.14),(.07,.35,.08),'wood')


def dummy(m,x,z,y=.2):
    m.box((x,y+.06,z),(.85,.12,.65),'timber')
    m.cylinder((x,y+.85,z),.065,1.65,'wood',n=10)
    m.cylinder((x,y+1.15,z),.28,.7,'linen',n=12)
    m.ellipsoid((x,y+1.8,z),(.24,.25,.23),'linen',n=12,rings=8)
    m.beam((x-.7,y+1.3,z),(x+.7,y+1.3,z),.09,'wood')


def anvil(m,x,z,y=.2):
    m.cylinder((x,y+.33,z),.35,.66,'wood',n=10)
    m.box((x,y+.79,z),(.65,.24,.38),'metal')
    m.cylinder((x+.47,y+.84,z),.19,.5,'metal',n=8,top=.02,axis=(1,0,0))
    m.box((x-.12,y+.59,z),(.24,.22,.25),'metal')


def furnace(m,x,z,width=2,y=.2):
    m.box((x,y+.27,z),(width+.3,.54,1.8),'stone')
    m.box((x,y+.60,z+.35),(width,.55,.95),'brick')
    m.box((x,y+.56,z-.1),(width*.72,.06,.7),'ember')
    for side in (-1,1):m.box((x+side*(width/2-.18),y+1.08,z),(.36,1.15,1.4),'brick')
    m.arch(x,y+.62,z-.57,width-.72,1.18,.22,.35,'brick')
    m.box((x,y+1.78,z),(width,.35,1.45),'brick')
    m.box((x,y+4.2,z+.23),(.85,4.7,.85),'brick')
    for side in (-1,1):
        m.box((x+side*.34,y+6.92,z+.23),(.17,.74,.85),'brick')
        m.box((x,y+6.92,z+.23+side*.34),(.51,.74,.17),'brick')
        m.box((x+side*.43,y+7.37,z+.23),(.22,.22,1.08),'trim')
        m.box((x,y+7.37,z+.23+side*.43),(.64,.22,.22),'trim')
    m.box((x,y+6.58,z+.23),(.51,.02,.51),'coal')
    for i in range(4):m.box((x+(i-1.5)*.21,y+.67,z-.30),(.09,.06,.18),'metal')


def bed(m,x,z,y=.2):
    m.box((x,y+.45,z),(1,.22,2),'wood')
    m.box((x,y+.64,z),(.94,.18,1.92),'linen')
    m.box((x,y+.77,z+.55),(.75,.15,.48),'linen')
    m.box((x,y+.77,z-.32),(.95,.05,1.0),'green')
    for a in (-1,1):
        for b in (-1,1):m.box((x+a*.41,y+.24,z+b*.84),(.1,.48,.1),'timber')
    m.box((x,y+.98,z+.94),(1.1,.8,.1),'wood')


def planter(m,x,z,w=1.5,d=.7,y=.2):
    m.box((x,y+.22,z),(w,.44,d),'clay')
    m.box((x,y+.45,z),(w*.9,.035,d*.84),'soil')
    for i in range(5):
        xx=x+(i-2)*w*.16
        m.beam((xx,y+.45,z),(xx,y+.98,z),.028,'wood')
        for s in (-1,1):m.ellipsoid((xx+s*.10,y+.77,z),(.15,.17,.12),'leaf',n=10,rings=6)


def tree(m,x,z,height=6,y=.2):
    m.cylinder((x,y+height*.31,z),height*.055,height*.62,'timber',n=12,top=height*.033)
    for i in range(5):
        a=i*math.tau/5
        tip=(x+math.cos(a)*height*.20,y+height*.72,z+math.sin(a)*height*.20)
        m.beam((x,y+height*.45,z),tip,height*.045,'timber')
        m.ellipsoid(tip,(height*.24,height*.25,height*.23),'leaf' if i%2 else 'leaflight',n=16,rings=10)


def statue(m,x,z,height=2.5,y=.2):
    m.box((x,y+.2,z),(.9,.4,.9),'trim')
    for side in (-1,1):m.beam((x+side*.15,y+.4,z),(x+side*.13,y+height*.44,z),.21,'trim')
    m.cylinder((x,y+height*.58,z),.29,height*.42,'trim',n=12,top=.38)
    m.ellipsoid((x,y+height*.91,z),(.23,.3,.23),'trim',n=12,rings=8)
    m.beam((x-.34,y+height*.73,z),(x-.48,y+height*.43,z-.05),.15,'trim')
    m.beam((x+.34,y+height*.73,z),(x+.5,y+height*.60,z-.15),.15,'trim')


def lantern(m,x,z,y=2):
    m.box((x,y,z),(.28,.48,.28),'amber')
    for sx in (-1,1):
        for sz in (-1,1):m.box((x+sx*.15,y,z+sz*.15),(.035,.54,.035),'metal')
    for yy in (-.27,.27):m.box((x,y+yy,z),(.37,.08,.37),'metal')
    m.beam((x,y+.31,z),(x,y+.75,z),.035,'metal')


def game_table(m,x,z,y=.2):
    m.cylinder((x,y+.76,z),1.0,.16,'wood',n=8)
    m.cylinder((x,y+.85,z),.85,.035,'green',n=8)
    m.cylinder((x,y+.36,z),.16,.72,'timber',n=10)
    m.cylinder((x,y+.08,z),.5,.16,'wood',n=10)
    for i in range(4):
        a=i*math.tau/4
        m.box((x+math.cos(a)*.53,y+.88,z+math.sin(a)*.53),(.14,.025,.21),'paper',yaw=-a)
        m.cylinder((x+math.cos(a+.3)*.34,y+.91,z+math.sin(a+.3)*.34),.075,.09,'red' if i%2 else 'blue',n=10)
        m.cylinder((x+math.cos(a)*1.3,y+.39,z+math.sin(a)*1.3),.28,.15,'red',n=12)
        m.cylinder((x+math.cos(a)*1.3,y+.2,z+math.sin(a)*1.3),.09,.4,'wood',n=10)


def telescope(m,x,z,y=6,length=2.7):
    direction=(0,.48,-math.sqrt(1-.48**2))
    def p(distance):return x,y+1.12+direction[1]*distance,z+direction[2]*distance
    m.cylinder(p(length/2-.40),.22,length,'bronze',n=32,axis=direction)
    for distance in (-.38,length*.32,length-.43):m.cylinder(p(distance),.26,.12,'metal',n=32,axis=direction)
    m.cylinder(p(length-.34),.225,.035,'roofglass',n=32,axis=direction)
    m.cylinder(p(-.58),.08,.35,'metal',n=20,axis=direction)
    m.cylinder((x,y+.49,z),.16,.94,'metal',n=20)
    m.cylinder((x,y+1.12,z),.28,.65,'bronze',n=24,axis=(1,0,0))
    for i in range(3):
        a=i*math.tau/3
        m.beam((x+math.cos(a)*.65,y,z+math.sin(a)*.65),(x,y+.78,z),.10,'metal')
        m.cylinder((x+math.cos(a)*.65,y+.025,z+math.sin(a)*.65),.14,.05,'metal',n=12)
    m.cylinder((x+.50,y+1.12,z),.21,.08,'metal',n=20,axis=(1,0,0))
