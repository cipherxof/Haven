using Haven.Parser.Geom;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Haven.Forms
{
    public partial class GeomFlagEditor : Form
    {
        public Geom Prim;

        public static ulong GEO_COL_A_TYPRECOIL = 0x2;  // 1st bit (LSB 0), if (attr & 0x2 != 0)
        public static ulong GEO_COL_A_TYPFLOOR = 0x4;  // 2nd bit, if (attr & 0x4 != 0)
        public static ulong GEO_COL_A_SOUND = 0x8;  // 3rd bit, if (attr & 0x8 != 0)
        public static ulong GEO_COL_A_PLAYER = 0x10;  // 4th bit, etc.
        public static ulong GEO_COL_A_ENEMY = 0x20;  // 5
        public static ulong GEO_COL_A_BULLET = 0x40;  // 6
        public static ulong GEO_COL_A_MISSILE = 0x80;  // 7
        public static ulong GEO_COL_A_BOMB = 0x100;  // 8
        public static ulong GEO_COL_A_RADOR = 0x200;  // 9
        public static ulong GEO_COL_A_BLOOD = 0x400;  //10
        public static ulong GEO_COL_A_IK = 0x800;  //11
        public static ulong GEO_COL_A_STAIRWAY = 0x1000;  //12
        public static ulong GEO_COL_A_STOP_EYE = 0x2000;  //13
        public static ulong GEO_COL_A_CLIFF = 0x4000;  //14
        public static ulong GEO_COL_A_TYPTHROUGH = 0x8000;  //15
        public static ulong GEO_COL_A_LEAN = 0x10000;  //16
        public static ulong GEO_COL_A_DONT_FALL = 0x20000;  //17
        public static ulong GEO_COL_A_CAMERA = 0x40000;  //18
        public static ulong GEO_COL_A_SHADOW = 0x80000;  //19
        public static ulong GEO_COL_A_INTRUDE = 0x100000;  //20
        public static ulong GEO_COL_A_ATTACK_GUARD = 0x200000;  //21
        public static ulong GEO_COL_A_CLIFF_FLOOR = 0x400000;  //22
        public static ulong GEO_COL_A_BULLET_MARK = 0x800000;  //23
        public static ulong GEO_COL_A_HEIGHT_LIMIT = 0x1000000;  //24
        public static ulong GEO_COL_A_NO_BEHIND = 0x2000000;  //25
        public static ulong GEO_COL_A_BEHIND_THROUGH = 0x4000000;  //26
        public static ulong GEO_COL_A_27 = 0x8000000; // 27
        public static ulong GEO_COL_A_28 = 0x10000000; // 28
        public static ulong GEO_COL_A_29 = 0x20000000; // 29
        public static ulong GEO_COL_A_30 = 0x40000000; // 30
        public static ulong GEO_COL_A_31 = 0x80000000; // 31

        public GeomFlagEditor(Geom prim)
        {
            InitializeComponent();

            Prim = prim;
        }

        private void UpdateAttributes(ulong attribute)
        {
            checkBox1.Checked = (attribute & GEO_COL_A_TYPRECOIL) != 0;
            checkBox2.Checked = (attribute & GEO_COL_A_TYPFLOOR) != 0;
            checkBox3.Checked = (attribute & GEO_COL_A_SOUND) != 0;

            checkBox5.Checked = (attribute & GEO_COL_A_PLAYER) != 0;
            checkBox6.Checked = (attribute & GEO_COL_A_ENEMY) != 0;
            checkBox7.Checked = (attribute & GEO_COL_A_BULLET) != 0;

            checkBox16.Checked = (attribute & GEO_COL_A_MISSILE) != 0;
            checkBox15.Checked = (attribute & GEO_COL_A_BOMB) != 0;
            checkBox14.Checked = (attribute & GEO_COL_A_RADOR) != 0;

            checkBox12.Checked = (attribute & GEO_COL_A_BLOOD) != 0;
            checkBox11.Checked = (attribute & GEO_COL_A_IK) != 0;
            checkBox10.Checked = (attribute & GEO_COL_A_STAIRWAY) != 0;

            checkBox24.Checked = (attribute & GEO_COL_A_STOP_EYE) != 0;
            checkBox23.Checked = (attribute & GEO_COL_A_CLIFF) != 0;
            checkBox22.Checked = (attribute & GEO_COL_A_TYPTHROUGH) != 0;

            checkBox20.Checked = (attribute & GEO_COL_A_LEAN) != 0;
            checkBox19.Checked = (attribute & GEO_COL_A_DONT_FALL) != 0;
            checkBox18.Checked = (attribute & GEO_COL_A_CAMERA) != 0;

            checkBox9.Checked = (attribute & GEO_COL_A_SHADOW) != 0;
            checkBox13.Checked = (attribute & GEO_COL_A_INTRUDE) != 0;
            checkBox8.Checked = (attribute & GEO_COL_A_ATTACK_GUARD) != 0;

            checkBox21.Checked = (attribute & GEO_COL_A_CLIFF_FLOOR) != 0;
            checkBox4.Checked = (attribute & GEO_COL_A_BULLET_MARK) != 0;
            checkBox17.Checked = (attribute & GEO_COL_A_HEIGHT_LIMIT) != 0;

            checkBox25.Checked = (attribute & GEO_COL_A_NO_BEHIND) != 0;
            checkBox26.Checked = (attribute & GEO_COL_A_BEHIND_THROUGH) != 0;
            checkBox27.Checked = (attribute & GEO_COL_A_27) != 0;

            checkBox28.Checked = (attribute & GEO_COL_A_28) != 0;
            checkBox29.Checked = (attribute & GEO_COL_A_29) != 0;
            checkBox30.Checked = (attribute & GEO_COL_A_30) != 0;

            checkBox31.Checked = (attribute & GEO_COL_A_31) != 0;
        }

        private void SetFlag(bool set, ref ulong attribute, ulong flag)
        {
            if (set)
            {
                attribute |= flag;
            }
            else
            {
                attribute &= ~flag;
            }
        }

        private ulong GenerateAttribute(ulong attribute)
        {
            SetFlag(checkBox1.Checked, ref attribute, GEO_COL_A_TYPRECOIL);
            SetFlag(checkBox2.Checked, ref attribute, GEO_COL_A_TYPFLOOR);
            SetFlag(checkBox3.Checked, ref attribute, GEO_COL_A_SOUND);

            SetFlag(checkBox5.Checked, ref attribute, GEO_COL_A_PLAYER);
            SetFlag(checkBox6.Checked, ref attribute, GEO_COL_A_ENEMY);
            SetFlag(checkBox7.Checked, ref attribute, GEO_COL_A_BULLET);

            SetFlag(checkBox16.Checked, ref attribute, GEO_COL_A_MISSILE);
            SetFlag(checkBox15.Checked, ref attribute, GEO_COL_A_BOMB);
            SetFlag(checkBox14.Checked, ref attribute, GEO_COL_A_RADOR);

            SetFlag(checkBox12.Checked, ref attribute, GEO_COL_A_BLOOD);
            SetFlag(checkBox11.Checked, ref attribute, GEO_COL_A_IK);
            SetFlag(checkBox10.Checked, ref attribute, GEO_COL_A_STAIRWAY);

            SetFlag(checkBox24.Checked, ref attribute, GEO_COL_A_STOP_EYE);
            SetFlag(checkBox23.Checked, ref attribute, GEO_COL_A_CLIFF);
            SetFlag(checkBox22.Checked, ref attribute, GEO_COL_A_TYPTHROUGH);

            SetFlag(checkBox20.Checked, ref attribute, GEO_COL_A_LEAN);
            SetFlag(checkBox19.Checked, ref attribute, GEO_COL_A_DONT_FALL);
            SetFlag(checkBox18.Checked, ref attribute, GEO_COL_A_CAMERA);

            SetFlag(checkBox9.Checked, ref attribute, GEO_COL_A_SHADOW);
            SetFlag(checkBox13.Checked, ref attribute, GEO_COL_A_INTRUDE);
            SetFlag(checkBox8.Checked, ref attribute, GEO_COL_A_ATTACK_GUARD);

            SetFlag(checkBox21.Checked, ref attribute, GEO_COL_A_CLIFF_FLOOR);
            SetFlag(checkBox4.Checked, ref attribute, GEO_COL_A_BULLET_MARK);
            SetFlag(checkBox17.Checked, ref attribute, GEO_COL_A_HEIGHT_LIMIT);

            SetFlag(checkBox25.Checked, ref attribute, GEO_COL_A_NO_BEHIND);
            SetFlag(checkBox26.Checked, ref attribute, GEO_COL_A_BEHIND_THROUGH);
            SetFlag(checkBox27.Checked, ref attribute, GEO_COL_A_27);

            SetFlag(checkBox28.Checked, ref attribute, GEO_COL_A_28);
            SetFlag(checkBox29.Checked, ref attribute, GEO_COL_A_29);
            SetFlag(checkBox30.Checked, ref attribute, GEO_COL_A_30);

            SetFlag(checkBox31.Checked, ref attribute, GEO_COL_A_31);

            return attribute;
        }

        private void GeomFlagEditor_Load(object sender, EventArgs e)
        {
            UpdateAttributes(Prim.Attribute);
        }

        private void GeomFlagEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            Prim.Attribute = GenerateAttribute(Prim.Attribute);
        }

    }
}
