# Process Journal

## 02.19.26 | Initial Idea

Limited access game! Demarcating time by making something only available for a small window every day. Inspired by Han's [Scent of Time](https://www.goodreads.com/book/show/35024337-the-scent-of-time), I want to just explore building an experience that is informed by the idea of daily ritual. 

Obviously this is being informed by both [Ritual of the Moon](https://karastone.itch.io/ritual-of-the-moon) and [Vesper.5](http://mightyvision.blogspot.com/2012/08/vesper5.html) but is different in some important ways. I will do a deeper precedent study on those in the next few days, but the main difference to note is the strict time requirement. In those two games you are encouraged to make it a daily practice, but you can complete your 'task' at any point during the day. I am curious how a forced time-commitment changes the experience. Like the difference between Christian and Islamic requirements for prayer? And is there any benefit of 'punishment' for missing the window? Feels a little to "you lost your streak," but may be worth considering. Maybe there is a building of the experience if you are coming back for the second, third, fourth, etc day in a row. Or maybe it just builds if it's your second, third, yadda, time.

As of now the concept is this: Once a day you have a five-minute (or less? we'll see) window to access a page online that asks you to type in any five-letter word. After you have typed the word, a unique small musical koan is played for you based on your particular combination of letters. You are then invited to come back tomorrow. 

That's it! Super simple, but I think it might be something? At the very least it will be something that I can interact with over a stretch of time and see how I feel about it. There's obviously ways it could get pushed further (are the little songs saved for you to access later? Do they build on eachother? Do you end up with a little word poem music thing after some set number of days?[^1]), but for now I'll try and keep it super narrowly focused.

I think it's important to note that the real impetus here is just to think about time and games in a thoughtful way, and this initial idea may end up not being the way forward. But, a good place to start the exploration I think.

Also to note: LAUTIR is just RITUAL backwards because it needed a name, and I'm an idiot.

## 02.20.26

![Letter Input Prototype](Media/letterInput.gif)

Built the basic bones of the time-requirement and letter-input aspects of this. Initially, I had planned on a five letter word (probably influenced by Wordle), but when I went to make the gif and was trying to think of what word to use, I realized that six letters would let me use LAUTIR, which feels like a smart move. Also, six letters will give us more musical-control options. 

I'm starting to think about what exactly this _looks_ like upon submission. Do the letters fade away? Do they dance away? Do they drift around to the music? Are they presented one by one to signify what they are contributing to, sonically? Probably relies a bit on what the audio ends up sounding like.

And with that! Next stage is building out the RNBO patch. I could obviously also go with vanilla Unity audio or FMOD, but I think RNBO gives me more granular control and weirder options. Hopefully I will remember how to integrate it all!

## 02.24.26

Discussed this with [Z](https://github.com/zSpaceSheikh), and she/we had some cool thoughts that I wanted to jot down real quick:

* Nature often makes us wait (the blooming of the corpse flower, the return of the cicadas, solar eclipses, meteor showers, Haley's comet) and it is always exciting when these come around 
* Similarly, there are times of day/month/year when things align (both in nature and in the built environment) - certain rock formations during the summer solstice, [Manhattanhenge](https://en.wikipedia.org/wiki/Manhattanhenge)
* In general, there is something to be said for _collectively_ waiting for something - live television is an example of this, waiting for a concert to start, the phenomena of [HQ Trivia](https://en.wikipedia.org/wiki/HQ_(video_game)) or Questlove's live DJ sets during lockdown

With this, could there be an alignment on the screen that happens as we get closer to the available time for the interaction? A possible "timer" that doesn't rely on the obvious clock countdown. The visual technique that has already been done with LTHC, IE, and eikon could be used here, but also: the glitcheffects plugin is both a) a bit too cumbersome/wonky in the browser, and b) no longer supported, so possibly making my own shader effects might be the way to go. 

## 02.28.26 | Alignment Prototype

[Playable Prototype](https://mouseandthebillionaire.github.io/lautir/0.2/)

![12 hours from the time](Media/twelveHours.gif)

Spent last week working on the alignment experiment. I repurposed the [eikon](https://mouseandthebillionaire.com/eikon/) circles, and switched the Unity colorspace to additive RBG, so that when they all line up in the middle (as we get closer to the target time) it creates a (mostly) white unified circle. 

As far as the actual playable prototype, it's kind of boring right now? There's just the circles to look at (though the word-entering-UI does appear if you are within the correct minute[^2]) For future versions, maybe it makes sense to have some kind of UI slider so that you can see what it looks like at various points of day? I don't know.

![1 minute from time](Media/oneMinute.gif)

I really like this idea of alignment serving as a visual cue to when the interaction is available. In general, this notion of alignment is super interesting, especially when placed in a non-organic, technological domain. I was surprised when reading the [Manhattanhenge](https://en.wikipedia.org/wiki/Manhattanhenge) Wikipedia page that there wasn't a specific name for this phenomenon, but L did some research which came up with "solar alignment," "collinearity," and, interestingly, "apophenia" (which is the human tendency to perceive meaningful patters, order, or connections in random or unrelated things).

Rudolf Otto's term "numinous" from _[The Idea of the Holy](https://archive.org/details/in.ernet.dli.2015.262513)_ also cropped up in this search which is something that I haven't thought about in a while. Not sure if it's going to play into this piece or the greater PhD ideas, but definitely something to chew on for a bit.

## Notes

[^1]: I actually reallllly like this idea. This also opens it up for being a defined length. Come back for seven days and you get a little thing at the end. Also reminds me a bit of the [A Series of Questions](https://github.com/mouseandthebillionaire/_sonicCharacteristics) project, but shoot me if I ever try and do audio-export from Unity again. Famous last words!

[^2]: 14:24 in this specific version
	
